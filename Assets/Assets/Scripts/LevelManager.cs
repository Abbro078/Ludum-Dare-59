using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Campaign Sequence")]
    [Tooltip("The sequence of days that defines the campaign.")]
    public CampaignSequence campaign;

    [Header("References")]
    [Tooltip("Reference to the spawner.")]
    public TrainJunctionSpawner spawner;

    public int CurrentDayIndex { get; private set; } = 0;
    
    private int trainsPassedToday = 0;
    private int trainsSpawnedToday = 0;
    private DaySettings currentDaySettings;
    private GameObject currentTrackLayoutInstance;
    private List<GameObject> extraMechanicsInstances = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartDay(CurrentDayIndex);
    }

    public void StartDay(int dayIndex)
    {
        if (campaign == null || campaign.days == null || dayIndex < 0 || dayIndex >= campaign.days.Count)
        {
            Debug.LogWarning("Invalid day index or no campaign sequence set.");
            return;
        }

        CurrentDayIndex = dayIndex;
        currentDaySettings = campaign.days[dayIndex];
        
        trainsPassedToday = 0;
        trainsSpawnedToday = 0;

        Debug.Log($"Starting {currentDaySettings.dayName}!");

        // 1. Clean up old instances if any
        CleanupDay();

        // 2. Spawn Track Layout
        JunctionLayout layoutToUse = null;
        if (currentDaySettings.trackLayoutPrefab != null)
        {
            currentTrackLayoutInstance = Instantiate(currentDaySettings.trackLayoutPrefab);
            layoutToUse = currentTrackLayoutInstance.GetComponent<JunctionLayout>();
        }
        else
        {
            // Try to find a default one in the scene
            layoutToUse = FindFirstObjectByType<JunctionLayout>();
        }

        if (layoutToUse == null)
        {
            Debug.LogError("No JunctionLayout found! Make sure a track layout prefab is assigned or one exists in the scene.");
            return;
        }

        // 3. Spawn Extra Mechanics
        if (currentDaySettings.extraMechanicsPrefabs != null)
        {
            foreach (var prefab in currentDaySettings.extraMechanicsPrefabs)
            {
                if (prefab != null)
                {
                    extraMechanicsInstances.Add(Instantiate(prefab));
                }
            }
        }

        // 4. Initialize Spawner
        if (spawner != null)
        {
            spawner.SetupDay(currentDaySettings, layoutToUse);
        }
    }

    public void OnTrainSpawned()
    {
        trainsSpawnedToday++;
    }

    public void OnTrainPassed()
    {
        trainsPassedToday++;

        // Also add score for legacy support
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddPoint();
        }

        CheckDayComplete();
    }

    private void CheckDayComplete()
    {
        if (currentDaySettings != null && trainsPassedToday >= currentDaySettings.requiredTrainsToPass)
        {
            Debug.Log($"{currentDaySettings.dayName} Complete!");
            
            // Wait for junction to clear naturally (the spawner has stopped spawning already)
            // For now, we instantly trigger day complete UI when the last train passes.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowDayCompleteUI(GameManager.Instance.Score);
            }
        }
    }

    public void NextDay()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HideDayCompleteUI();
        }
        
        StartDay(CurrentDayIndex + 1);
    }

    private void CleanupDay()
    {
        if (currentTrackLayoutInstance != null)
        {
            Destroy(currentTrackLayoutInstance);
            currentTrackLayoutInstance = null;
        }

        foreach (var mechanics in extraMechanicsInstances)
        {
            if (mechanics != null)
            {
                Destroy(mechanics);
            }
        }
        extraMechanicsInstances.Clear();
    }
}
