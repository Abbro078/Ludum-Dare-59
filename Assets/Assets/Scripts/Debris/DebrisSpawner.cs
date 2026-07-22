using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;


public class DebrisSpawner : MonoBehaviour
{
    [Header("Warning Effect (Optional)")]
    [Tooltip("An optional particle effect or UI visual to play before the debris drops in.")]
    public GameObject warningEffectPrefab;
    [Tooltip("How long to play the warning effect before spawning the actual debris.")]
    public float warningDuration = 2f;

    private void Start()
    {
        if (TrainJunctionSpawner.Instance != null)
        {
            TrainJunctionSpawner.Instance.OnWaveStarted += HandleWaveStarted;
        }
    }

    private void OnDestroy()
    {
        if (TrainJunctionSpawner.Instance != null)
        {
            TrainJunctionSpawner.Instance.OnWaveStarted -= HandleWaveStarted;
        }
    }

    private void HandleWaveStarted()
    {
        if (TrainJunctionSpawner.Instance == null || TrainJunctionSpawner.Instance.CurrentDaySettings == null) return;
        
        DebrisConfig config = TrainJunctionSpawner.Instance.CurrentDaySettings.debrisConfig;
        
        if (!config.enableDebrisThisDay) return;

        StartCoroutine(SpawnDebrisRoutine(config));
    }

    private IEnumerator SpawnDebrisRoutine(DebrisConfig config)
    {
        if (config.debrisPrefabs == null || config.debrisPrefabs.Count == 0)
        {
            Debug.LogWarning("[DebrisSpawner] No debris prefabs assigned in DaySettings!");
            yield break;
        }

        JunctionLayout layout = FindFirstObjectByType<JunctionLayout>();
        if (layout == null || layout.entryPoints == null || layout.entryPoints.Count == 0)
        {
            Debug.LogWarning("[DebrisSpawner] Could not find a valid JunctionLayout in the scene.");
            yield break;
        }

        List<CinemachinePath> allPaths = new List<CinemachinePath>();
        foreach (var entry in layout.entryPoints)
        {
            if (entry.routesFromHere == null) continue;
            foreach (var route in entry.routesFromHere)
            {
                if (route.path != null && !allPaths.Contains(route.path))
                {
                    allPaths.Add(route.path);
                }
            }
        }

        if (allPaths.Count == 0) yield break;

        int spawnCount = Random.Range(config.minDebrisPerWave, config.maxDebrisPerWave + 1);

        List<Vector3> spawnLocations = new List<Vector3>();

        for (int i = 0; i < spawnCount; i++)
        {
            CinemachinePath randomPath = allPaths[Random.Range(0, allPaths.Count)];
            
            float randomNormalizedDistance = Random.Range(0.45f, 0.55f);
            
            float actualDistance = randomNormalizedDistance * randomPath.PathLength;
            Vector3 position = randomPath.EvaluatePositionAtUnit(actualDistance, CinemachinePathBase.PositionUnits.Distance);
            
            spawnLocations.Add(position);

            if (warningEffectPrefab != null)
            {
                GameObject warningObj = Instantiate(warningEffectPrefab, position, Quaternion.identity);
                Destroy(warningObj, warningDuration);
            }
        }

        if (warningDuration > 0f && warningEffectPrefab != null)
        {
            yield return new WaitForSeconds(warningDuration);
        }

        foreach (Vector3 pos in spawnLocations)
        {
            GameObject prefab = config.debrisPrefabs[Random.Range(0, config.debrisPrefabs.Count)];
            GameObject spawnedDebris = Instantiate(prefab, pos, Quaternion.identity, transform);
            
            if (config.overrideRequiredClicks > 0)
            {
                ClickableDebris clickable = spawnedDebris.GetComponent<ClickableDebris>();
                if (clickable != null)
                {
                    clickable.SetRequiredClicks(config.overrideRequiredClicks);
                }
            }
        }
    }
}
