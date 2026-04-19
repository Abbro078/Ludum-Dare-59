using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;

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
    }

    [System.Serializable]
    public class EntryPoint
    {
        [Tooltip("Where this entrance is located.")]
        public TrackDirection entrance; 
        
        [Tooltip("Optional gate controller for this entrance to stop/start trains.")]
        public CrossingGate gate;

        [Tooltip("The routes that explicitly originate from this entrance.")]
        public List<RouteMap> routesFromHere = new List<RouteMap>();
    }

    [Header("Spawner Configurations")]
    [Tooltip("Interval between spawning cycles in seconds")]
    public float spawnCycleDuration = 5f;
    
    [Tooltip("The prefab of the train to spawn. Needs a TrainController attached.")]
    public GameObject trainPrefab;

    [Header("4-Way Junction Logic")]
    [Tooltip("Define your 4 Entry Points here (Top, Bottom, Left, Right). Assign the 3 routes that start at each entrance.")]
    public List<EntryPoint> entryPoints = new List<EntryPoint>();

    private float timer;

    void Start()
    {
        timer = spawnCycleDuration;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        
        if (timer <= 0f)
        {
            SpawnTrains();
            timer = spawnCycleDuration;
        }
    }

    private void SpawnTrains()
    {
        if (trainPrefab == null)
        {
            Debug.LogError("Train prefab is not assigned in TrainJunctionSpawner!");
            return;
        }

        // We need at least 3 distinct entry points set up to avoid overlap
        if (entryPoints.Count < 3)
        {
            Debug.LogWarning("Insufficient Entry Points! Define at least 3 Entry Points to stop trains from spawning inside each other.");
            return;
        }

        // 1. Shuffle and pick 3 distinct Entry Points
        List<EntryPoint> randomizedEntries = new List<EntryPoint>(entryPoints);
        for (int i = 0; i < randomizedEntries.Count; i++)
        {
            int rnd = Random.Range(i, randomizedEntries.Count);
            EntryPoint temp = randomizedEntries[i];
            randomizedEntries[i] = randomizedEntries[rnd];
            randomizedEntries[rnd] = temp;
        }

        // 2. Spawn 1 train at 3 of those selected entrances
        for (int i = 0; i < 3; i++)
        {
            EntryPoint selectedEntry = randomizedEntries[i];
            
            if (selectedEntry.routesFromHere == null || selectedEntry.routesFromHere.Count == 0) continue;

            // Pick 1 random route specifically from this entrance
            int randomRouteIndex = Random.Range(0, selectedEntry.routesFromHere.Count);
            RouteMap chosenRoute = selectedEntry.routesFromHere[randomRouteIndex];

            if (chosenRoute.path == null) continue;

            // Spawn train at the start position of the track
            Vector3 startPos = chosenRoute.path.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Distance);
            GameObject spawnedTrain = Instantiate(trainPrefab, startPos, Quaternion.identity);
            
            TrainController controller = spawnedTrain.GetComponent<TrainController>();
            if (controller != null)
            {
                controller.AssignRoute(chosenRoute.path, selectedEntry.gate);
            }
            else
            {
                Debug.LogWarning("TrainController component not found on the spawned train prefab!");
            }
        }
    }
}
