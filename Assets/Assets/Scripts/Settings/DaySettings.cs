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
    [Tooltip("Starting time between each wave.")]
    public float initialTimeBetweenWaves = 6f;
    
    [Tooltip("How much time is subtracted from the wave timer after every successful wave, making the day progressively harder.")]
    public float timeBetweenWavesDecreaseRate = 0.1f;
    
    [Tooltip("The lowest the timer can possibly go during this day.")]
    public float minimumTimeBetweenWaves = 3f;

    [Header("Day-Specific Content & Mechanics")]
    [Tooltip("List of train prefabs allowed to spawn on this day.")]
    public List<GameObject> allowedTrainTypes;
    
    [Tooltip("(Optional) The Layout Prefab for this day. Contains the tracks, visual environment, and JunctionLayout component. If null, the game assumes there's a default one in the scene.")]
    public GameObject trackLayoutPrefab;
    
    [Tooltip("(Optional) Prefabs for new mechanics introduced this day (e.g., an Obstacle Spawner). These will be instantiated at the start of the day and destroyed at the end.")]
    public List<GameObject> extraMechanicsPrefabs;
}
