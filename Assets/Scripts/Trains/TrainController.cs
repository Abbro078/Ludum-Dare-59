using UnityEngine;
using Unity.Cinemachine;

public class TrainController : MonoBehaviour
{
    [Header("Train Setup")]
    [Tooltip("Drag and drop your CinemachineDollyCart objects here.")]
    [SerializeField] private CinemachineDollyCart[] carts;

    private CinemachinePath assignedRoute;
    private CrossingGate currentGate;
    private float[] originalSpeeds;

    // True once this train has fully passed the crossing zone
    private bool hasClearedZone = false;

    /// <summary>
    /// Assigns a route to the manually assigned Dolly Carts.
    /// Also hooks into an optional crossing gate to stop/start movement.
    /// </summary>
    /// <param name="route">The CinemachinePath track to follow.</param>
    /// <param name="gate">The gate controller at this spawn entrance (can be null).</param>
    public void AssignRoute(CinemachinePath route, CrossingGate gate = null)
    {
        assignedRoute = route;
        currentGate = gate;
        
        if (carts == null || carts.Length == 0)
        {
            Debug.LogWarning("TrainController has no carts assigned in the Inspector!", this);
            return;
        }

        originalSpeeds = new float[carts.Length];
        
        for (int i = 0; i < carts.Length; i++)
        {
            if (carts[i] != null)
            {
                // Assign the path, but leave m_Position exactly as you set it in the Inspector!
                carts[i].m_Path = route;
                
                // Cache the speed so we can restore it later if the train is stopped by a gate
                originalSpeeds[i] = carts[i].m_Speed; 
            }
        }

        // Hook up to Gate events if there is one
        if (currentGate != null)
        {
            if (!currentGate.isGateOpen)
            {
                StopTrainCarts();
            }

            currentGate.OnGateOpened.AddListener(ResumeTrainCarts);
            currentGate.OnGateClosed.AddListener(StopTrainCarts);
        }
    }

    // -----------------------------------------------------------
    // Crossing-zone detection via trigger collider on the gate
    // -----------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (hasClearedZone) return;

        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null && gate == currentGate)
        {
            gate.RegisterTrainInZone(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (hasClearedZone) return;

        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null && gate == currentGate)
        {
            hasClearedZone = true;
            gate.UnregisterTrainFromZone(this);

            // Detach from gate events — this train has passed and must not be stopped
            gate.OnGateOpened.RemoveListener(ResumeTrainCarts);
            gate.OnGateClosed.RemoveListener(StopTrainCarts);

            // Ensure the train is running at full speed regardless of gate state
            ResumeTrainCarts();
        }
    }

    private void StopTrainCarts()
    {
        if (carts == null) return;
        for (int i = 0; i < carts.Length; i++)
        {
            if (carts[i] != null) 
            {
                carts[i].m_Speed = 0f;
            }
        }
    }

    private void ResumeTrainCarts()
    {
        if (carts == null) return;
        for (int i = 0; i < carts.Length; i++)
        {
            if (carts[i] != null) 
            {
                carts[i].m_Speed = originalSpeeds[i];
            }
        }
    }

    private void OnDestroy()
    {
        // Always clean up listeners appropriately
        if (currentGate != null)
        {
            currentGate.UnregisterTrainFromZone(this);
            currentGate.OnGateOpened.RemoveListener(ResumeTrainCarts);
            currentGate.OnGateClosed.RemoveListener(StopTrainCarts);
        }
    }
}