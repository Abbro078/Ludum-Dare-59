using UnityEngine;
using Unity.Cinemachine;

public abstract class TrainBase : MonoBehaviour
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
    
    [Header("UI Indication")]
    [Tooltip("The icon shown on the warning UI before this train spawns.")]
    public Sprite trainWarningIcon;

    [Tooltip("Speed the carts travel along the path.")]
    public float[] originalSpeeds;
    private float[] originalPositions;
    private System.Action<TrainBase> onCompleteCallback;
    private System.Collections.Generic.HashSet<CrossingGate> occupiedGates = new System.Collections.Generic.HashSet<CrossingGate>();
    private int cartsInGateZone = 0;

    private bool hasClearedZone = false;
    private bool hasScored = false;
    protected bool IsStoppedWaitingForGate = false;

    private void Awake()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            Debug.LogWarning($"[TrainBase] No Rigidbody found on {gameObject.name}! Collisions between trains will NOT work. Please add a Rigidbody (set to Is Kinematic).");
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

    public void AssignRoute(CinemachinePath route, CrossingGate gate = null, System.Action<TrainBase> onComplete = null)
    {
        assignedRoute = route;
        currentGate = gate;
        onCompleteCallback = onComplete;
        
        occupiedGates.Clear();
        hasScored = false;
        hasClearedZone = false;
        IsStoppedWaitingForGate = false;
        
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
        }
        
        if (carts == null || carts.Length == 0)
        {
            Debug.LogWarning("TrainBase has no carts assigned in the Inspector!", this);
            return;
        }
        
        for (int i = 0; i < carts.Length; i++)
        {
            if (carts[i] != null)
            {
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
        
        if (currentGate != null)
        {
            currentGate.OnGateOpened.AddListener(ResumeTrainCarts);
        }
    }

    private void Update()
    {
        if (assignedRoute != null && carts != null && carts.Length > 0 && !hasScored)
        {
            CinemachineDollyCart firstCart = carts[0];
            if (firstCart != null && firstCart.m_Position >= assignedRoute.PathLength)
            {
                hasScored = true;
                
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.OnTrainPassed();
                }
                else if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddPoint();
                }
                
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

        TrainBase otherTrain = other.GetComponentInParent<TrainBase>();
        if (otherTrain != null && otherTrain != this)
        {
            Debug.Log($"Train {gameObject.name} collided with {otherTrain.gameObject.name}!");
            PlayCrashSound();
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
            return;
        }

        DebrisBase debris = other.GetComponentInParent<DebrisBase>();
        if (debris != null)
        {
            Debug.Log($"Train {gameObject.name} crashed into debris!");
            PlayCrashSound();
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
            return;
        }

        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null)
        {
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

    protected void PlayCrashSound()
    {
        if (crashSfx != null)
        {
            GameObject sfxObj = new GameObject("CrashSFX");
            AudioSource src = sfxObj.AddComponent<AudioSource>();
            src.clip = crashSfx;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;

            float skipTime = 1f;
            if (crashSfx.length > skipTime)
            {
                src.time = skipTime;
            }

            src.Play();
            
            Destroy(sfxObj, crashSfx.length - src.time + 0.1f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TrainBase otherTrain = collision.collider.GetComponentInParent<TrainBase>();
        if (otherTrain != null && otherTrain != this)
        {
            Debug.Log($"Train {gameObject.name} physically crashed into {otherTrain.gameObject.name}!");
            PlayCrashSound();
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
            return;
        }

        DebrisBase debris = collision.collider.GetComponentInParent<DebrisBase>();
        if (debris != null)
        {
            Debug.Log($"Train {gameObject.name} physically crashed into debris!");
            PlayCrashSound();
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
            return;
        }
    }

    public void HandleTriggerExit(Collider other)
    {
        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null)
        {
            cartsInGateZone--;

            if (cartsInGateZone <= 0)
            {
                cartsInGateZone = 0;

                occupiedGates.Remove(gate);
                gate.UnregisterTrainFromZone(this);

                if (gate == currentGate && !hasClearedZone)
                {
                    hasClearedZone = true;
                    gate.OnGateOpened.RemoveListener(ResumeTrainCarts);
                    ResumeTrainCarts();
                    DisableAllColliders();
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CrossingGate gate = other.GetComponentInParent<CrossingGate>();
        if (gate != null)
        {
            occupiedGates.Remove(gate);
            gate.UnregisterTrainFromZone(this);

            if (gate == currentGate && !hasClearedZone)
            {
                hasClearedZone = true;

                gate.OnGateOpened.RemoveListener(ResumeTrainCarts);

                ResumeTrainCarts();
                DisableAllColliders();
            }
        }
    }

    private void DisableAllColliders()
    {
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
    }

    protected virtual void StopTrainCarts()
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

        if (wasMoving && trainAudioSource != null)
        {
            trainAudioSource.Stop();
        }

        if (currentGate != null && !IsStoppedWaitingForGate)
        {
            IsStoppedWaitingForGate = true;
            OnStoppedAtGate(currentGate);
        }
    }

    private System.Collections.IEnumerator PlayStoppingSoundRoutine()
    {
        trainAudioSource.Stop();
        trainAudioSource.clip = stoppingSfx;
        trainAudioSource.loop = false;
        trainAudioSource.Play();
        
        yield return new WaitForSeconds(1f);
        
        if (trainAudioSource.clip == stoppingSfx)
        {
            trainAudioSource.Stop();
        }
    }

    private void ResumeTrainCarts()
    {
        if (carts == null) return;

        bool wasStopped = false;
        for (int i = 0; i < carts.Length; i++)
        {
            if (carts[i] != null)
            {
                if (carts[i].m_Speed == 0f) wasStopped = true;
                carts[i].m_Speed = originalSpeeds[i];
            }
        }

        if (wasStopped && trainAudioSource != null && movingSfx != null)
        {
            trainAudioSource.clip = movingSfx;
            trainAudioSource.Play();
        }

        if (currentGate != null && IsStoppedWaitingForGate)
        {
            IsStoppedWaitingForGate = false;
            OnResumedFromGate(currentGate);
        }
    }

    protected virtual void OnStoppedAtGate(CrossingGate gate) { }
    protected virtual void OnResumedFromGate(CrossingGate gate) { }

    private void OnDisable()
    {
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
