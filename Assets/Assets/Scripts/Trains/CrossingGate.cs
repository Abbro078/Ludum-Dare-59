using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem; // Required for the New Input System!
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Place this on your physical Gate GameObject.
/// Attach a CHILD GameObject with a trigger Collider that spans the full crossing zone
/// and tag it "CrossingZone" — trains entering/exiting that trigger are tracked here.
/// </summary>
public class CrossingGate : MonoBehaviour
{
    [Header("Gate State")]
    public bool isGateOpen = true;

    [Header("Animation")]
    [Tooltip("Animator on the gate model. Leave empty to skip animation.")]
    public Animator gateAnimator;

    [Tooltip("Exact name of the Opening animation state in the Animator.")]
    public string openStateName  = "Opening";

    [Tooltip("Exact name of the Closing animation state in the Animator.")]
    public string closeStateName = "Closing";

    [Tooltip("Exact name of the Idle animation state in the Animator.")]
    public string idleStateName  = "Nothinging";
    
    [Tooltip("Exact name of the Idle Open animation state in the Animator.")]
    public string idleStateOpen  = "OpenNothinging";
    
    [Header("Visual/Event Feedback")]
    [Tooltip("Fires when the gate is opened (use to play animation, change material, etc.)")]
    public UnityEvent OnGateOpened;
    
    [Tooltip("Fires when the gate is closed")]
    public UnityEvent OnGateClosed;

    [Header("UI & Audio Feedback")]
    [Tooltip("An Image (set to type Filled) to show the remaining closed time. Will be toggled on/off automatically.")]
    public UnityEngine.UI.Image timerFillImage;

    [Tooltip("AudioSource to play gate sounds.")]
    public AudioSource gateAudioSource;
    [Tooltip("Sound played when the player clicks the gate.")]
    public AudioClip clickSfx;

    [Header("Hover Feedback")]
    [Tooltip("Fires when the mouse cursor hovers over the gate.")]
    public UnityEvent OnGateHoverEnter;

    [Tooltip("Fires when the mouse cursor leaves the gate.")]
    public UnityEvent OnGateHoverExit;

    private float closedTimer = 0f;
    private float maxForceClosedTime = 0f;
    private bool isForceTimerActive = false;
    private Coroutine animCoroutine;
    private bool isHovered = false;

    private HashSet<TrainBase> trainsInZone = new HashSet<TrainBase>();

    void Start()
    {
        if (timerFillImage != null)
        {
            timerFillImage.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!isGateOpen && isForceTimerActive)
        {
            closedTimer -= Time.deltaTime;

            if (timerFillImage != null)
            {
                timerFillImage.fillAmount = closedTimer / maxForceClosedTime;
            }

            if (closedTimer <= 0)
            {
                isForceTimerActive = false;
                SetGateOpen(true);
            }
        }

        if (Mouse.current != null)
        {
            CheckForMouseHover();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckForMouseClick();
            }
        }
    }
    
    private void CheckForMouseClick()
    {
        if (Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
    
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            CrossingGate clickedGate = hit.collider.GetComponentInParent<CrossingGate>();

            if (clickedGate == this)
            {
                if (gateAudioSource != null && clickSfx != null)
                {
                    gateAudioSource.PlayOneShot(clickSfx);
                }
                ToggleGate();
            }
        }
    }

    private void CheckForMouseHover()
    {
        if (Camera.main == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        bool hitThis = false;
        if (Physics.Raycast(ray, out hit))
        {
            CrossingGate hoveredGate = hit.collider.GetComponentInParent<CrossingGate>();
            if (hoveredGate == this)
            {
                hitThis = true;
            }
        }

        if (hitThis && !isHovered)
        {
            isHovered = true;
            OnGateHoverEnter?.Invoke();
        }
        else if (!hitThis && isHovered)
        {
            isHovered = false;
            OnGateHoverExit?.Invoke();
        }
    }

    public void RegisterTrainInZone(TrainBase train)
    {
        trainsInZone.Add(train);
    }

    public void UnregisterTrainFromZone(TrainBase train)
    {
        trainsInZone.Remove(train);
    }

    public bool IsOccupied => trainsInZone.Count > 0;
    
    public void ToggleGate()
    {
        SetGateOpen(!isGateOpen);
    }

    public void SetGateOpen(bool open)
    {
        if (isGateOpen == open) return;

        if (!open && IsOccupied)
        {
            Debug.Log($"{gameObject.name}: Cannot close — a train is currently in the crossing zone!");
            return;
        }

        isGateOpen = open;

        if (!isGateOpen)
        {
            OnGateClosed?.Invoke();
            PlayAnimation(closeStateName, nextStateName: idleStateName);
            Debug.Log($"{gameObject.name} Closed! Trains waiting...");
        }
        else
        {
            isForceTimerActive = false;
            if (timerFillImage != null)
            {
                timerFillImage.gameObject.SetActive(false);
            }

            OnGateOpened?.Invoke();
            PlayAnimation(openStateName, nextStateName: idleStateOpen);
            Debug.Log($"{gameObject.name} Opened! Trains moving.");
        }
    }
    
    public void StartForceOpenTimer(float duration)
    {
        if (isGateOpen) return;

        maxForceClosedTime = duration;
        closedTimer = duration;
        isForceTimerActive = true;

        if (timerFillImage != null)
        {
            timerFillImage.fillAmount = 1f;
            timerFillImage.gameObject.SetActive(true);
        }
    }

    public void StopForceOpenTimer()
    {
        isForceTimerActive = false;
        
        if (timerFillImage != null)
        {
            timerFillImage.gameObject.SetActive(false);
        }
    }

    private void PlayAnimation(string stateName, string nextStateName)
    {
        if (gateAnimator == null) return;

        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        gateAnimator.speed = 1f;
        gateAnimator.Play(stateName, 0, 0f);

        animCoroutine = StartCoroutine(WaitThenPlay(stateName, nextStateName));
    }

    private IEnumerator WaitThenPlay(string stateName, string nextStateName)
    {
        yield return null;

        while (true)
        {
            AnimatorStateInfo info = gateAnimator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(stateName) && info.normalizedTime >= 1f)
                break;
            yield return null;
        }

        gateAnimator.speed = 1f;
        gateAnimator.Play(nextStateName, 0, 0f);
        animCoroutine = null;
    }
}
