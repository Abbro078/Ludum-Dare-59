using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewDaySettings", menuName = "Game/Day Settings")]
public class DaySettings : ScriptableObject
{
    [Header("Day Information")]
    public string dayName = "Day 1";
    
    [Header("Win Condition")]
    [Tooltip("Number of trains that must safely pass to complete the day.")]
    public int requiredTrainsToPass = 20;

    [Header("Spawning Mechanics")]
    [Tooltip("Minimum number of trains that can spawn at the exact same time.")]
    public int minSimultaneousSpawns = 1;
    
    [Tooltip("Maximum number of trains that can spawn at the exact same time.")]
    public int maxSimultaneousSpawns = 2;
    
    [Header("Progressive Difficulty (Intra-Day)")]
    [Tooltip("Time between each wave of trains. This stays fixed to prevent overlapping trains.")]
    public float timeBetweenWaves = 8f;
    
    [Tooltip("Starting duration of the UI warning before a train spawns.")]
    public float initialWarningDuration = 6f;
    
    [Tooltip("How much time is subtracted from the warning duration after every successful wave. This reduces the player's reaction time!")]
    public float warningDurationDecreaseRate = 0.1f;
    
    [Tooltip("The shortest possible warning time during this day.")]
    public float minimumWarningDuration = 2f;

    [Header("Day-Specific Content & Mechanics")]
    [Tooltip("List of train prefabs allowed to spawn on this day.")]
    public List<GameObject> allowedTrainTypes;
    
    [Tooltip("(Optional) The Layout Prefab for this day. Contains the tracks, visual environment, and JunctionLayout component. If null, the game assumes there's a default one in the scene.")]
    public GameObject trackLayoutPrefab;
    
    [Tooltip("(Optional) Prefabs for new mechanics introduced this day (e.g., an Obstacle Spawner). These will be instantiated at the start of the day and destroyed at the end.")]
    public List<GameObject> extraMechanicsPrefabs;

    [Header("Debris Settings")]
    [Tooltip("Configure debris for this specific day.")]
    public DebrisConfig debrisConfig;
}

[System.Serializable]
public struct DebrisConfig
{
    public bool enableDebrisThisDay;
    [Tooltip("Prefabs to randomly spawn. Must have a DebrisBase component.")]
    public List<GameObject> debrisPrefabs;
    public int minDebrisPerWave;
    public int maxDebrisPerWave;
    [Tooltip("If greater than 0, overrides the required clicks on the prefab to scale difficulty.")]
    public int overrideRequiredClicks;
}
