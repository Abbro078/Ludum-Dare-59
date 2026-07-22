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
    
    [Tooltip("Optional parent transform to instantiate the track layout and mechanics inside of.")]
    public Transform trackContainer;

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

        CleanupDay();

        JunctionLayout layoutToUse = null;
        if (currentDaySettings.trackLayoutPrefab != null)
        {
            currentTrackLayoutInstance = Instantiate(currentDaySettings.trackLayoutPrefab, trackContainer);
            layoutToUse = currentTrackLayoutInstance.GetComponent<JunctionLayout>();
        }
        else
        {
            layoutToUse = FindFirstObjectByType<JunctionLayout>();
        }

        if (layoutToUse == null)
        {
            Debug.LogError("No JunctionLayout found! Make sure a track layout prefab is assigned or one exists in the scene.");
            return;
        }

        if (currentDaySettings.extraMechanicsPrefabs != null)
        {
            foreach (var prefab in currentDaySettings.extraMechanicsPrefabs)
            {
                if (prefab != null)
                {
                    extraMechanicsInstances.Add(Instantiate(prefab, trackContainer));
                }
            }
        }

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
