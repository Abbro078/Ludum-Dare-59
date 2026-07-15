using UnityEngine;
using System.Collections.Generic;

public class JunctionLayout : MonoBehaviour
{
    [Header("4-Way Junction Logic")]
    [Tooltip("Define your 4 Entry Points here (Top, Bottom, Left, Right). Assign the routes that start at each entrance.")]
    public List<TrainJunctionSpawner.EntryPoint> entryPoints = new List<TrainJunctionSpawner.EntryPoint>();
}
