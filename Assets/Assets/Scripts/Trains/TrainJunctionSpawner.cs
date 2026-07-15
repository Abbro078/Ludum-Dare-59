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

    [Header("Object Pooling")]
    [Tooltip("Default initial number of trains to keep in the pool (per type).")]
    public int defaultPoolCapacity = 10;
    
    [Tooltip("Maximum number of trains to store in the pool before destroying excess.")]
    public int maxPoolSize = 20;

    [Header("Warning UI")]
    [Tooltip("Reference to the TrainWarningUI component in the scene.")]
    public TrainWarningUI warningUI;

    // ─────────────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────────────

    private float timer;
    private float currentCycleDuration;
    private DaySettings currentDaySettings;
    private JunctionLayout activeLayout;
    private bool isSpawningActive = false;
    private int trainsSpawnedThisDay = 0;
    private Dictionary<GameObject, UnityEngine.Pool.ObjectPool<TrainController>> trainPools = new Dictionary<GameObject, UnityEngine.Pool.ObjectPool<TrainController>>();

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Pools are now initialized on demand per-prefab
    }

    void Start()
    {
        // Spawning will be started by LevelManager.SetupDay()
    }

    public void SetupDay(DaySettings daySettings, JunctionLayout layout)
    {
        currentDaySettings = daySettings;
        activeLayout = layout;
        currentCycleDuration = daySettings.initialTimeBetweenWaves;
        trainsSpawnedThisDay = 0;
        isSpawningActive = true;
        
        // Start with a short gap before the very first warning sequence kicks off
        timer = 2f;
    }

    void Update()
    {
        if (!isSpawningActive || currentDaySettings == null || activeLayout == null) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            TriggerWarnings();
            
            // Decrease cycle duration for progressive difficulty
            currentCycleDuration = Mathf.Max(
                currentDaySettings.minimumTimeBetweenWaves, 
                currentCycleDuration - currentDaySettings.timeBetweenWavesDecreaseRate
            );
            timer = currentCycleDuration;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Warning + Spawn logic
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Randomly selects up to 3 distinct entry+route pairs ensuring no topological deadlock
    /// (so the player always has a way to solve the puzzle), and fires a warning UI for each.
    /// The actual train instantiation is deferred to the warning's completion callback.
    /// <summary>
    private void TriggerWarnings()
    {

        // We need at least enough entry points to satisfy the max spawn
        if (activeLayout.entryPoints.Count < currentDaySettings.maxSimultaneousSpawns)
        {
            Debug.LogWarning("Insufficient Entry Points in JunctionLayout!");
            return;
        }

        // Shuffle a copy so we don't mutate the original list
        List<EntryPoint> shuffled = new List<EntryPoint>(activeLayout.entryPoints);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int        rnd  = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[rnd]) = (shuffled[rnd], shuffled[i]);
        }

        // Determine how many trains to spawn this wave
        int trainsToSpawn = Random.Range(currentDaySettings.minSimultaneousSpawns, currentDaySettings.maxSimultaneousSpawns + 1);

        // Cap by remaining trains for the day
        int remainingTrainsForDay = currentDaySettings.requiredTrainsToPass - trainsSpawnedThisDay;
        trainsToSpawn = Mathf.Min(trainsToSpawn, remainingTrainsForDay);

        if (trainsToSpawn <= 0)
        {
            isSpawningActive = false;
            return; // We have spawned all required trains for the day!
        }

        // Grab exactly trainsToSpawn entry points (that actually have routes configured)
        List<EntryPoint> activeEntries = new List<EntryPoint>();
        for (int i = 0; i < shuffled.Count && activeEntries.Count < trainsToSpawn; i++)
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
            trainsSpawnedThisDay++;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnTrainSpawned();
            }
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

    // ─────────────────────────────────────────────────────────────────────────
    // Object Pooling & Actual Spawning
    // ─────────────────────────────────────────────────────────────────────────

    private UnityEngine.Pool.ObjectPool<TrainController> GetOrCreatePool(GameObject prefab)
    {
        if (!trainPools.ContainsKey(prefab))
        {
            trainPools[prefab] = new UnityEngine.Pool.ObjectPool<TrainController>(
                createFunc: () => {
                    GameObject obj = Instantiate(prefab);
                    return obj.GetComponent<TrainController>();
                },
                actionOnGet: (train) => train.gameObject.SetActive(true),
                actionOnRelease: (train) => train.gameObject.SetActive(false),
                actionOnDestroy: (train) => Destroy(train.gameObject),
                collectionCheck: false,
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolSize
            );
        }
        return trainPools[prefab];
    }

    /// <summary>
    /// Grabs a train from the pool, places it at the start, and hands it off.
    /// </summary>
    private void SpawnTrain(EntryPoint entry, RouteMap route)
    {
        if (currentDaySettings == null || currentDaySettings.allowedTrainTypes == null || currentDaySettings.allowedTrainTypes.Count == 0 || route.path == null) 
        {
            Debug.LogWarning("[TrainJunctionSpawner] Cannot spawn train. No allowed train types defined in current DaySettings.");
            return;
        }

        GameObject prefabToSpawn = currentDaySettings.allowedTrainTypes[Random.Range(0, currentDaySettings.allowedTrainTypes.Count)];
        if (prefabToSpawn == null) return;

        UnityEngine.Pool.ObjectPool<TrainController> pool = GetOrCreatePool(prefabToSpawn);
        TrainController controller = pool.Get();
        
        if (controller != null)
        {
            Vector3 startPos = route.path.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Distance);
            
            // Explicitly set the top-level transform
            controller.transform.position = startPos;
            controller.transform.rotation = Quaternion.identity;

            // Pass the path, the gate, and the callback to return it to the correct pool
            controller.AssignRoute(route.path, entry.gate, (train) => pool.Release(train));
        }
        else
        {
            Debug.LogWarning("[TrainJunctionSpawner] TrainController component not found on the spawned train prefab!");
        }
    }
}
