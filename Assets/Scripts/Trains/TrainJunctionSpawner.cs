using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;

/// <summary>
/// Manages timed train spawning at a 4-way junction, with a pre-spawn UI warning sequence.
///
/// The Spawner simply triggers the warning UI manager every <see cref="spawnCycleDuration"/> seconds.
/// The <see cref="TrainWarningUI"/> handles the exact timings (warning sign, route show, train spawn).
/// </summary>
public class TrainJunctionSpawner : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Enums & nested types
    // ─────────────────────────────────────────────────────────────────────────

    public enum TrackDirection
    {
        Top,
        Bottom,
        Left,
        Right
    }

    [System.Serializable]
    public class RouteMap
    {
        [Tooltip("The direction this route heads towards.")]
        public TrackDirection destination;

        public CinemachinePath path;

        [Tooltip("Distinct sprite shown in the warning UI to represent this specific route.")]
        public Sprite routeSprite;
    }

    [System.Serializable]
    public class EntryPoint
    {
        [Tooltip("Where this entrance is located on the junction.")]
        public TrackDirection entrance;

        [Tooltip("Optional gate controller for this entrance to stop/start trains.")]
        public CrossingGate gate;

        [Tooltip("The routes that explicitly originate from this entrance.")]
        public List<RouteMap> routesFromHere = new List<RouteMap>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Spawner Configurations")]
    [Tooltip("The prefab of the train to spawn. Needs a TrainController attached.")]
    public GameObject trainPrefab;

    [Header("Warning UI")]
    [Tooltip("Reference to the TrainWarningUI component in the scene.")]
    public TrainWarningUI warningUI;

    [Header("Timing")]
    [Tooltip("Total time between each wave of warnings. (e.g. 8 seconds allows for a 6-second UI warning + 2 seconds of cooldown).")]
    public float spawnCycleDuration = 8f;

    [Header("4-Way Junction Logic")]
    [Tooltip("Define your 4 Entry Points here (Top, Bottom, Left, Right). Assign the routes that start at each entrance.")]
    public List<EntryPoint> entryPoints = new List<EntryPoint>();

    // ─────────────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────────────

    private float timer;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Start with a 2-second gap before the very first warning sequence kicks off
        timer = 2f;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            TriggerWarnings();
            timer = spawnCycleDuration;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Warning + Spawn logic
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Randomly selects up to 3 distinct entry+route pairs ensuring no topological deadlock
    /// (so the player always has a way to solve the puzzle), and fires a warning UI for each.
    /// The actual train instantiation is deferred to the warning's completion callback.
    /// </summary>
    private void TriggerWarnings()
    {
        if (trainPrefab == null)
        {
            Debug.LogError("Train prefab is not assigned in TrainJunctionSpawner!");
            return;
        }

        // We need at least 3 distinct entry points to avoid trains spawning on top of each other
        if (entryPoints.Count < 3)
        {
            Debug.LogWarning("Insufficient Entry Points! Define at least 3 Entry Points.");
            return;
        }

        // Shuffle a copy so we don't mutate the original list
        List<EntryPoint> shuffled = new List<EntryPoint>(entryPoints);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int        rnd  = Random.Range(i, shuffled.Count);
            EntryPoint temp = shuffled[i];
            shuffled[i]     = shuffled[rnd];
            shuffled[rnd]   = temp;
        }

        // Grab exactly 3 entry points (that actually have routes configured)
        List<EntryPoint> activeEntries = new List<EntryPoint>();
        for (int i = 0; i < 3; i++)
        {
            if (shuffled[i].routesFromHere != null && shuffled[i].routesFromHere.Count > 0)
            {
                activeEntries.Add(shuffled[i]);
            }
        }

        if (activeEntries.Count == 0) return;

        List<RouteMap> chosenRoutes = new List<RouteMap>();

        // Try to generate a valid (deadlock-free) combination of routes
        bool foundValid = false;
        int maxAttempts = 50;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            chosenRoutes.Clear();
            foreach (var entry in activeEntries)
            {
                int rnd = Random.Range(0, entry.routesFromHere.Count);
                chosenRoutes.Add(entry.routesFromHere[rnd]);
            }

            if (IsValidCombination(activeEntries, chosenRoutes))
            {
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            Debug.LogWarning("[TrainJunctionSpawner] Hard to find a deadlock-free set. Using the last random set.");
        }

        // Spawn warnings for the selected routes
        for (int i = 0; i < activeEntries.Count; i++)
        {
            EntryPoint capturedEntry = activeEntries[i];
            RouteMap   capturedRoute = chosenRoutes[i];

            if (capturedRoute.path == null) continue;

            ShowWarning(capturedEntry, capturedRoute);
        }
    }

    /// <summary>
    /// Checks if a set of routes creates a deadlock.
    /// A deadlock occurs if trains form a cycle of dependencies (e.g., Top -> Bottom AND Bottom -> Top).
    /// If there are cycles, the player cannot let either train enter the junction without crashing into the other.
    /// </summary>
    private bool IsValidCombination(List<EntryPoint> activeEntries, List<RouteMap> chosenRoutes)
    {
        // remainingTrains maps: start entrance -> desired destination
        List<KeyValuePair<TrackDirection, TrackDirection>> remainingTrains = new List<KeyValuePair<TrackDirection, TrackDirection>>();
        for (int i = 0; i < activeEntries.Count; i++)
        {
            remainingTrains.Add(new KeyValuePair<TrackDirection, TrackDirection>(activeEntries[i].entrance, chosenRoutes[i].destination));
        }

        bool progressed = true;
        while (progressed && remainingTrains.Count > 0)
        {
            progressed = false;
            
            // Try to find a train that can leave safely (its destination is empty / not occupied by another waiting train)
            for (int i = 0; i < remainingTrains.Count; i++)
            {
                TrackDirection destination = remainingTrains[i].Value;
                bool destinationBlocked = false;

                // Is any *other* train waiting at 'destination'?
                for (int j = 0; j < remainingTrains.Count; j++)
                {
                    if (i != j && remainingTrains[j].Key == destination)
                    {
                        destinationBlocked = true;
                        break;
                    }
                }

                if (!destinationBlocked)
                {
                    // This train has a clear exit, simulate it leaving the junction
                    remainingTrains.RemoveAt(i);
                    progressed = true;
                    break;
                }
            }
        }

        // If all trains can eventually leave, the combination is solvable by the player!
        return remainingTrains.Count == 0;
    }

    /// <summary>
    /// Tells the UI manager to start the warning sequence for one entry+route pair.
    /// Falls back to immediate spawn if the manager is missing.
    /// </summary>
    private void ShowWarning(EntryPoint entry, RouteMap route)
    {
        if (warningUI == null)
        {
            Debug.LogWarning("[TrainJunctionSpawner] No warningUI assigned — spawning train immediately.");
            SpawnTrain(entry, route);
            return;
        }

        warningUI.ShowWarning(
            entry.entrance,
            route.routeSprite,
            () => SpawnTrain(entry, route)
        );
    }

    /// <summary>
    /// Instantiates the train prefab at the start of <paramref name="route"/>'s path
    /// and hands it off to <see cref="TrainController"/>.
    /// </summary>
    private void SpawnTrain(EntryPoint entry, RouteMap route)
    {
        if (trainPrefab == null || route.path == null) return;

        Vector3    startPos    = route.path.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Distance);
        GameObject spawnedTrain = Instantiate(trainPrefab, startPos, Quaternion.identity);

        TrainController controller = spawnedTrain.GetComponent<TrainController>();
        if (controller != null)
        {
            controller.AssignRoute(route.path, entry.gate);
        }
        else
        {
            Debug.LogWarning("[TrainJunctionSpawner] TrainController component not found on the spawned train prefab!");
        }
    }
}
