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
    
    [Tooltip("How long (in seconds) the gate can remain closed before forcing itself open.")]
    public float maxClosedTime = 5f;

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

    private float closedTimer = 0f;
    private Coroutine animCoroutine;

    // Trains currently occupying the crossing zone
    private HashSet<TrainController> trainsInZone = new HashSet<TrainController>();

    void Update()
    {
        // 1. Handle the automatic timer
        if (!isGateOpen)
        {
            closedTimer -= Time.deltaTime;
            if (closedTimer <= 0)
            {
                // Force open the gate when the timer runs out!
                SetGateOpen(true);
            }
        }

        // 2. Listen for the New Input System left mouse click
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckForMouseClick();
        }
    }
    
    /// <summary>
    /// Shoots a raycast from the camera to the mouse cursor to see if we clicked THIS object or its children.
    /// </summary>
    private void CheckForMouseClick()
    {
        // Make sure your camera in the hierarchy is tagged as "MainCamera"!
        if (Camera.main == null) return;

        // Get the current mouse position on the screen
        Vector2 mousePos = Mouse.current.position.ReadValue();
    
        // Create a 3D ray going out from the camera through that mouse position
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        // If the ray hits a 3D collider...
        if (Physics.Raycast(ray, out hit))
        {
            // Check if the collider we hit has a CrossingGate script on it, or on any of its parent objects
            CrossingGate clickedGate = hit.collider.GetComponentInParent<CrossingGate>();

            // If it found a script, and that script is THIS specific instance, toggle it!
            if (clickedGate == this)
            {
                ToggleGate();
            }
        }
    }

    // -------------------------------------------------------
    // Crossing-zone occupancy (called by TrainController)
    // -------------------------------------------------------

    /// <summary>Called when a train enters the crossing zone trigger.</summary>
    public void RegisterTrainInZone(TrainController train)
    {
        trainsInZone.Add(train);
    }

    /// <summary>Called when a train exits the crossing zone trigger (has passed).</summary>
    public void UnregisterTrainFromZone(TrainController train)
    {
        trainsInZone.Remove(train);
    }

    /// <summary>True when at least one train is currently inside the crossing zone.</summary>
    public bool IsOccupied => trainsInZone.Count > 0;

    // -------------------------------------------------------

    /// <summary>
    /// Flips the state of the gate.
    /// </summary>
    public void ToggleGate()
    {
        SetGateOpen(!isGateOpen);
    }

    public void SetGateOpen(bool open)
    {
        if (isGateOpen == open) return;

        // Block closing if a train is currently inside the crossing zone
        if (!open && IsOccupied)
        {
            Debug.Log($"{gameObject.name}: Cannot close — a train is currently in the crossing zone!");
            return;
        }

        isGateOpen = open;

        if (!isGateOpen)
        {
            closedTimer = maxClosedTime;
            OnGateClosed?.Invoke();
            PlayAnimation(closeStateName, nextStateName: idleStateName);
            Debug.Log($"{gameObject.name} Closed! Trains waiting... Timer started.");
        }
        else
        {
            OnGateOpened?.Invoke();
            PlayAnimation(openStateName, nextStateName: idleStateOpen);
            Debug.Log($"{gameObject.name} Opened! Trains moving.");
        }
    }

    // -------------------------------------------------------
    // Animation helpers
    // -------------------------------------------------------

    /// <summary>
    /// Plays <paramref name="stateName"/>, then transitions to <paramref name="nextStateName"/> once it finishes.
    /// </summary>
    private void PlayAnimation(string stateName, string nextStateName)
    {
        if (gateAnimator == null) return;

        // Cancel any in-progress animation coroutine
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        gateAnimator.speed = 1f;
        gateAnimator.Play(stateName, 0, 0f);

        animCoroutine = StartCoroutine(WaitThenPlay(stateName, nextStateName));
    }

    /// <summary>
    /// Waits for <paramref name="stateName"/> to finish, then plays <paramref name="nextStateName"/>.
    /// </summary>
    private IEnumerator WaitThenPlay(string stateName, string nextStateName)
    {
        // Give Unity one frame to register the new state
        yield return null;

        // Poll until we are inside the expected state and it has reached its end
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