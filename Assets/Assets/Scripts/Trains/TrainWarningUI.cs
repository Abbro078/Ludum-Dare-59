using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// Manages the warning display for trains spawning. 
/// Placed on a single Canvas object that contains the 4 directional images.
/// 
/// Sequence:
///   1. [Phase 1] Show the generic warning sprite in the corresponding direction's Image.
///   2. [Phase 2] Swap the sprite in that same Image to the route's destination sprite.
///   3. At the end of phase 2, hide the image and spawn the train.
/// </summary>
public class TrainWarningUI : MonoBehaviour
{
    [Header("UI References — Direction Sprites")]
    [SerializeField] private Image topTrackImage;
    [SerializeField] private Image rightTrackImage;
    [SerializeField] private Image bottomTrackImage;
    [SerializeField] private Image leftTrackImage;

    [SerializeField] private CanvasGroup topImageCanvasGroup;
    [SerializeField] private CanvasGroup rightImageCanvasGroup;
    [SerializeField] private CanvasGroup bottomImageCanvasGroup;
    [SerializeField] private CanvasGroup leftImageCanvasGroup;

    [Header("Sprites")]
    [Tooltip("The generic warning sprite shown during Phase 1.")]
    [SerializeField] private Sprite warningSprite;

    [Header("Timing")]
    [Tooltip("Seconds to display the generic warning sign (phase 1).")]
    [SerializeField] private float warningSignDuration = 1f;

    [Tooltip("Seconds to display the route-specific direction sprite (phase 2).")]
    [SerializeField] private float routeShowDuration = 5f;

    [Header("Audio")]
    [Tooltip("Sound effect played when the warning sign first appears.")]
    [SerializeField] private AudioClip warningSfx;
    [Tooltip("AudioSource to play the warning SFX. Uses the one on this GameObject if not assigned.")]
    [SerializeField] private AudioSource audioSource;

    void Start()
    {
        // Hide all directions initially
        SetGroupAlpha(topImageCanvasGroup,    0f);
        SetGroupAlpha(rightImageCanvasGroup,  0f);
        SetGroupAlpha(bottomImageCanvasGroup, 0f);
        SetGroupAlpha(leftImageCanvasGroup,   0f);
    }

    /// <summary>
    /// Called by the Spawner to start the warning sequence for a specific entrance.
    /// </summary>
    public void ShowWarning(
        TrainJunctionSpawner.TrackDirection arrivalDirection,
        Sprite routeSprite,
        Action onSpawn)
    {
        GetActiveReferences(arrivalDirection, out Image activeImage, out CanvasGroup activeGroup);

        if (activeImage != null && activeGroup != null)
        {
            StartCoroutine(RunSequence(activeImage, activeGroup, routeSprite, onSpawn));
        }
        else
        {
            // Fallback if the references aren't assigned
            onSpawn?.Invoke();
        }
    }

    private void GetActiveReferences(TrainJunctionSpawner.TrackDirection direction, out Image img, out CanvasGroup group)
    {
        switch (direction)
        {
            case TrainJunctionSpawner.TrackDirection.Top:
                img = topTrackImage;
                group = topImageCanvasGroup;
                break;
            case TrainJunctionSpawner.TrackDirection.Right:
                img = rightTrackImage;
                group = rightImageCanvasGroup;
                break;
            case TrainJunctionSpawner.TrackDirection.Bottom:
                img = bottomTrackImage;
                group = bottomImageCanvasGroup;
                break;
            case TrainJunctionSpawner.TrackDirection.Left:
                img = leftTrackImage;
                group = leftImageCanvasGroup;
                break;
            default:
                img = null;
                group = null;
                break;
        }
    }

    private IEnumerator RunSequence(Image activeImage, CanvasGroup activeGroup, Sprite destinationRouteSprite, Action onSpawn)
    {
        // ── Ensure AudioSource is hooked up ──
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // ── Show the warning sprite ──
        activeImage.sprite = warningSprite;
        activeGroup.alpha = 1f;

        // ── Play warning sound ──
        if (audioSource != null && warningSfx != null)
        {
            audioSource.PlayOneShot(warningSfx);
        }

        // ── Phase 1: Wait while the warning sign is shown ──
        yield return new WaitForSeconds(warningSignDuration);

        // ── Swap sprite instantly: warning sign → route destination sprite ──
        activeImage.sprite = destinationRouteSprite;

        // ── Phase 2: Hold route sprite for routeShowDuration ──
        yield return new WaitForSeconds(routeShowDuration);

        // ── Spawn the train ──
        onSpawn?.Invoke();

        // ── Hide the UI for this direction ──
        activeGroup.alpha = 0f;
    }

    private static void SetGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group != null) group.alpha = alpha;
    }
}
