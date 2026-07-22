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

    [Header("Object Pooling")]
    [Tooltip("Default initial number of trains to keep in the pool (per type).")]
    public int defaultPoolCapacity = 10;
    
    public static TrainJunctionSpawner Instance { get; private set; }

    [Tooltip("Maximum number of trains to store in the pool before destroying excess.")]
    public int maxPoolSize = 20;

    [Header("UI & Layout")]
    [Tooltip("The UI manager that shows warnings and track directions.")]
    public TrainWarningUI warningUI;

    public System.Action OnWaveStarted;

    private float timer;
    private float currentWarningDuration;
    private DaySettings currentDaySettings;
    public DaySettings CurrentDaySettings => currentDaySettings;
    private JunctionLayout activeLayout;
    private bool isSpawningActive = false;
    private int trainsSpawnedThisDay = 0;
    private Dictionary<GameObject, UnityEngine.Pool.ObjectPool<TrainBase>> trainPools = new Dictionary<GameObject, UnityEngine.Pool.ObjectPool<TrainBase>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Spawning will be started by LevelManager.SetupDay()
    }

    public void SetupDay(DaySettings daySettings, JunctionLayout layout)
    {
        currentDaySettings = daySettings;
        activeLayout = layout;
        currentWarningDuration = daySettings.initialWarningDuration;
        trainsSpawnedThisDay = 0;
        isSpawningActive = true;
        
        timer = 2f;
    }

    void Update()
    {
        if (!isSpawningActive || currentDaySettings == null || activeLayout == null) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            TriggerWarnings();
            
            currentWarningDuration = Mathf.Max(
                currentDaySettings.minimumWarningDuration, 
                currentWarningDuration - currentDaySettings.warningDurationDecreaseRate
            );
            
            timer = currentDaySettings.timeBetweenWaves;
        }
    }
    
    private void TriggerWarnings()
    {

        if (activeLayout.entryPoints.Count < currentDaySettings.maxSimultaneousSpawns)
        {
            Debug.LogWarning("Insufficient Entry Points in JunctionLayout!");
            return;
        }

        List<EntryPoint> shuffled = new List<EntryPoint>(activeLayout.entryPoints);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int        rnd  = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[rnd]) = (shuffled[rnd], shuffled[i]);
        }

        int trainsToSpawn = Random.Range(currentDaySettings.minSimultaneousSpawns, currentDaySettings.maxSimultaneousSpawns + 1);

        int remainingTrainsForDay = currentDaySettings.requiredTrainsToPass - trainsSpawnedThisDay;
        trainsToSpawn = Mathf.Min(trainsToSpawn, remainingTrainsForDay);

        if (trainsToSpawn <= 0)
        {
            isSpawningActive = false;
            return;
        }

        List<EntryPoint> activeEntries = new List<EntryPoint>();
        for (int i = 0; i < shuffled.Count && activeEntries.Count < trainsToSpawn; i++)
        {
            if (shuffled[i].routesFromHere != null && shuffled[i].routesFromHere.Count > 0)
            {
                activeEntries.Add(shuffled[i]);
            }
        }

        if (activeEntries.Count == 0) return;

        OnWaveStarted?.Invoke();

        List<RouteMap> chosenRoutes = new List<RouteMap>();

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

        bool spawnedUnstoppableThisWave = false;

        for (int i = 0; i < activeEntries.Count; i++)
        {
            EntryPoint capturedEntry = activeEntries[i];
            RouteMap   capturedRoute = chosenRoutes[i];

            if (capturedRoute.path == null) continue;
            
            if (currentDaySettings.allowedTrainTypes == null || currentDaySettings.allowedTrainTypes.Count == 0) 
            {
                Debug.LogWarning("[TrainJunctionSpawner] Cannot spawn train. No allowed train types defined in current DaySettings.");
                continue;
            }

            GameObject prefabToSpawn = null;
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                prefabToSpawn = currentDaySettings.allowedTrainTypes[Random.Range(0, currentDaySettings.allowedTrainTypes.Count)];
                
                bool isUnstoppable = prefabToSpawn.GetComponent<UnstoppableTrain>() != null;
                
                if (isUnstoppable)
                {
                    if (spawnedUnstoppableThisWave)
                    {
                        continue;
                    }
                    else
                    {
                        spawnedUnstoppableThisWave = true;
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            ShowWarning(capturedEntry, capturedRoute, prefabToSpawn);
            trainsSpawnedThisDay++;
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnTrainSpawned();
            }
        }
    }
    
    private bool IsValidCombination(List<EntryPoint> activeEntries, List<RouteMap> chosenRoutes)
    {
        List<KeyValuePair<TrackDirection, TrackDirection>> remainingTrains = new List<KeyValuePair<TrackDirection, TrackDirection>>();
        for (int i = 0; i < activeEntries.Count; i++)
        {
            remainingTrains.Add(new KeyValuePair<TrackDirection, TrackDirection>(activeEntries[i].entrance, chosenRoutes[i].destination));
        }

        bool progressed = true;
        while (progressed && remainingTrains.Count > 0)
        {
            progressed = false;
            
            for (int i = 0; i < remainingTrains.Count; i++)
            {
                TrackDirection destination = remainingTrains[i].Value;
                bool destinationBlocked = false;

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
                    remainingTrains.RemoveAt(i);
                    progressed = true;
                    break;
                }
            }
        }

        return remainingTrains.Count == 0;
    }

    private void ShowWarning(EntryPoint entry, RouteMap route, GameObject prefabToSpawn)
    {
        if (warningUI == null)
        {
            Debug.LogWarning("[TrainJunctionSpawner] No warningUI assigned — spawning train immediately.");
            SpawnTrain(entry, route, prefabToSpawn);
            return;
        }

        Sprite trainIcon = null;
        if (prefabToSpawn != null)
        {
            TrainBase tb = prefabToSpawn.GetComponent<TrainBase>();
            if (tb != null) trainIcon = tb.trainWarningIcon;
        }

        warningUI.ShowWarning(
            entry.entrance,
            trainIcon,
            route.routeSprite,
            currentWarningDuration,
            () => SpawnTrain(entry, route, prefabToSpawn)
        );
    }

    private UnityEngine.Pool.ObjectPool<TrainBase> GetOrCreatePool(GameObject prefab)
    {
        if (!trainPools.ContainsKey(prefab))
        {
            trainPools[prefab] = new UnityEngine.Pool.ObjectPool<TrainBase>(
                createFunc: () => {
                    GameObject obj = Instantiate(prefab);
                    return obj.GetComponent<TrainBase>();
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

    private void SpawnTrain(EntryPoint entry, RouteMap route, GameObject prefabToSpawn)
    {
        if (prefabToSpawn == null || route.path == null) return;

        UnityEngine.Pool.ObjectPool<TrainBase> pool = GetOrCreatePool(prefabToSpawn);
        TrainBase controller = pool.Get();
        
        if (controller != null)
        {
            Vector3 startPos = route.path.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Distance);
            
            controller.transform.position = startPos;
            controller.transform.rotation = Quaternion.identity;

            controller.AssignRoute(route.path, entry.gate, (train) => pool.Release(train));
        }
        else
        {
            Debug.LogWarning("[TrainJunctionSpawner] TrainBase component not found on the spawned train prefab!");
        }
    }
}
