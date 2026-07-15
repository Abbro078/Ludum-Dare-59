using UnityEngine;
using Unity.Cinemachine;

public class TrainController : MonoBehaviour
{
    [Header("Train Setup")]
    [Tooltip("Drag and drop your CinemachineDollyCart objects here.")]
    [SerializeField] private CinemachineDollyCart[] carts;

    [Header("Audio")]
    [Tooltip("AudioSource component on the Train root.")]
    [SerializeField] private AudioSource trainAudioSource;
    [Tooltip("Looping SFX played while the train is moving.")]
    [SerializeField] private AudioClip movingSfx;
    [Tooltip("SFX played when the train stops at a closed gate.")]
    [SerializeField] private AudioClip stoppingSfx;
    [Tooltip("SFX played when the train crashes.")]
    [SerializeField] private AudioClip crashSfx;

    private CinemachinePath assignedRoute;
    private CrossingGate currentGate;
    private float[] originalSpeeds;
    private float[] originalPositions;
    private System.Action<TrainController> onCompleteCallback;
    private System.Collections.Generic.HashSet<CrossingGate> occupiedGates = new System.Collections.Generic.HashSet<CrossingGate>();
    private int cartsInGateZone = 0;

    // True once this train has fully passed the crossing zone
    private bool hasClearedZone = false;
    private bool hasScored = false;

    private void Awake()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            Debug.LogWarning($"[TrainController] No Rigidbody found on {gameObject.name}! Collisions between trains will NOT work. Please add a Rigidbody (set to Is Kinematic).");
        }

        if (carts != null && carts.Length > 0)
        {
            originalSpeeds = new float[carts.Length];
            originalPositions = new float[carts.Length];
            for (int i = 0; i < carts.Length; i++)
            {
                if (carts[i] != null) 
                {
                    originalSpeeds[i] = carts[i].m_Speed;
                    originalPositions[i] = carts[i].m_Position;
                }
            }
        }
    }

    /// <summary>
    /// Assigns a route to the manually assigned Dolly Carts.
    /// Also hooks into an optional crossing gate to stop/start movement.
    /// </summary>
    /// <param name="route">The CinemachinePath track to follow.</param>
    /// <param name="onComplete">Callback triggered when the train is successfully done.</param>
    public void AssignRoute(CinemachinePath route, CrossingGate gate = null, System.Action<TrainController> onComplete = null)
    {
        assignedRoute = route;
        currentGate = gate;
        onCompleteCallback = onComplete;
        
        occupiedGates.Clear();
        hasScored = false;
        hasClearedZone = false;
        
        if (carts == null || carts.Length == 0)
        {
            Debug.LogWarning("TrainController has no carts assigned in the Inspector!", this);
            return;
        }
        
        for (int i = 0; i < carts.Length; i++)
        {
            if (carts[i] != null)
            {
                // Assign path and restore its original offset so trains don't overlap!
                carts[i].m_Path = route;
                carts[i].m_Position = originalPositions[i];
                carts[i].m_Speed = originalSpeeds[i];
            }
        }

        if (trainAudioSource == null) trainAudioSource = GetComponent<AudioSource>();
        if (trainAudioSource != null && movingSfx != null)
        {
            trainAudioSource.clip = movingSfx;
            trainAudioSource.loop = true;
            if (trainAudioSource.gameObject.activeInHierarchy) trainAudioSource.Play();
        }

        // Hook up to Gate events if there is one.
        // We only listen for OnGateOpened so that a train waiting at the gate
        // resumes when the gate opens. We do NOT subscribe to OnGateClosed here
        // because the train should keep moving until it physically reaches the gate.
        if (currentGate != null)
        {
            currentGate.OnGateOpened.AddListener(ResumeTrainCarts);
        }
    }

    // -----------------------------------------------------------
    // Gameplay Logic & Physics
    // -----------------------------------------------------------

    private void Update()
    {
        if (assignedRoute != null && carts != null && carts.Length > 0 && !hasScored)
        {
            // Check if the first cart has reached the end of the path
            CinemachineDollyCart firstCart = carts[0];
            if (firstCart != null && firstCart.m_Position >= assignedRoute.PathLength)
            {
                hasScored = true;
                
                // Yield a point and clean up
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.OnTrainPassed();
                }
                else if (GameManager.Instance != null)
                {
                    // Fallback just in case LevelManager isn't there
                    GameManager.Instance.AddPoint();
                }
                
                // Tell the Spawner to return this train into the pool
                if (onCompleteCallback != null)
                {
                    onCompleteCallback.Invoke(this);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    public void HandleCollision(Collider other)
    {
        if (hasClearedZone) return;

        // 1. Train-to-Train collision check
        TrainController otherTrain = other.GetComponentInParent<TrainController>();
        if (otherTrain != null && otherTrain != this)
        {
            Debug.Log($"Train {gameObject.name} collided with {otherTrain.gameObject.name}!");
            PlayCrashSound();
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
            return;
        }

        // 2. Gate interaction check
        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null)
        {
            // A cart entered the zone! Count it up.
            cartsInGateZone++; 
            
            occupiedGates.Add(gate);
            gate.RegisterTrainInZone(this);

            if (gate == currentGate)
            {
                if (!gate.isGateOpen) StopTrainCarts();
            }
            else
            {
                if (!gate.isGateOpen)
                {
                    Debug.Log($"Train {gameObject.name} crashed into closed gate {gate.gameObject.name}!");
                    PlayCrashSound();
                    if (GameManager.Instance != null) GameManager.Instance.GameOver();
                }
            }
        }
    }

    private void PlayCrashSound()
    {
        if (crashSfx != null)
        {
            // Create a temporary 2D AudioSource to guarantee it's heard regardless of camera distance or TimeScale
            GameObject sfxObj = new GameObject("CrashSFX");
            AudioSource src = sfxObj.AddComponent<AudioSource>();
            src.clip = crashSfx;
            src.spatialBlend = 0f; // 2D sound (max volume everywhere)
            src.ignoreListenerPause = true;

            // Start playing the clip exactly from the 1.0 second mark
            float skipTime = 1f;
            if (crashSfx.length > skipTime)
            {
                src.time = skipTime;
            }

            src.Play();
            
            // Destroy the object after the REMAINING length of the clip
            Destroy(sfxObj, crashSfx.length - src.time + 0.1f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Fallback in case your colliders are solid (Is Trigger = false)
        TrainController otherTrain = collision.collider.GetComponentInParent<TrainController>();
        if (otherTrain != null && otherTrain != this)
        {
            Debug.Log($"Train {gameObject.name} physically crashed into {otherTrain.gameObject.name}!");
            PlayCrashSound();
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
        }
    }

    // New method called by TrainCart.cs
    public void HandleTriggerExit(Collider other)
    {
        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null)
        {
            // A cart left the zone! Count it down.
            cartsInGateZone--;

            // ONLY execute the "exit" logic if the VERY LAST cart has left the zone
            if (cartsInGateZone <= 0)
            {
                cartsInGateZone = 0; // Safety clamp to prevent negative numbers

                occupiedGates.Remove(gate);
                gate.UnregisterTrainFromZone(this);

                if (gate == currentGate && !hasClearedZone)
                {
                    hasClearedZone = true;
                    gate.OnGateOpened.RemoveListener(ResumeTrainCarts);
                    ResumeTrainCarts();
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null)
        {
            // ALWAYS unregister occupancy when we leave the gate
            occupiedGates.Remove(gate);
            gate.UnregisterTrainFromZone(this);

            if (gate == currentGate && !hasClearedZone)
            {
                hasClearedZone = true;

                // Detach from entrance gate events — this train has passed and must not be stopped
                gate.OnGateOpened.RemoveListener(ResumeTrainCarts);

                // Ensure the train is running at full speed regardless of gate state
                ResumeTrainCarts();
            }
        }
    }

    private void StopTrainCarts()
    {
        if (carts == null) return;
        
        bool wasMoving = false;
        for (int i = 0; i < carts.Length; i++)
        {
            if (carts[i] != null) 
            {
                if (carts[i].m_Speed > 0f) wasMoving = true;
                carts[i].m_Speed = 0f;
            }
        }

        if (wasMoving && trainAudioSource != null && stoppingSfx != null)
        {
            StartCoroutine(PlayStoppingSoundRoutine());
        }
    }

    private System.Collections.IEnumerator PlayStoppingSoundRoutine()
    {
        trainAudioSource.Stop();
        trainAudioSource.clip = stoppingSfx;
        trainAudioSource.loop = false;
        trainAudioSource.Play();
        
        // Let it play for EXACTLY 1 second
        yield return new WaitForSeconds(1f);
        
        // Cut it off if it's still playing the stopping sfx
        if (trainAudioSource.clip == stoppingSfx)
        {
            trainAudioSource.Stop();
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

        if (trainAudioSource != null && movingSfx != null)
        {
            trainAudioSource.clip = movingSfx;
            trainAudioSource.loop = true;
            if (!trainAudioSource.isPlaying && trainAudioSource.gameObject.activeInHierarchy) 
                trainAudioSource.Play();
        }
    }

    private void OnDisable()
    {
        // Safe cleanup for object pooling
        foreach (var gate in occupiedGates)
        {
            if (gate != null) gate.UnregisterTrainFromZone(this);
        }
        occupiedGates.Clear();

        if (currentGate != null)
        {
            currentGate.OnGateOpened.RemoveListener(ResumeTrainCarts);
        }
    }
}