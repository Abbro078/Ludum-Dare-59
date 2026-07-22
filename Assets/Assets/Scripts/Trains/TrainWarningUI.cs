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

    private int[] activeWarningsCount = new int[4];

    void Start()
    {
        SetGroupAlpha(topImageCanvasGroup,    0f);
        SetGroupAlpha(rightImageCanvasGroup,  0f);
        SetGroupAlpha(bottomImageCanvasGroup, 0f);
        SetGroupAlpha(leftImageCanvasGroup,   0f);
    }

 
    public void ShowWarning(
        TrainJunctionSpawner.TrackDirection arrivalDirection,
        Sprite trainWarningSprite,
        Sprite routeSprite,
        float totalWarningDuration,
        Action onSpawn)
    {
        GetActiveReferences(arrivalDirection, out Image activeImage, out CanvasGroup activeGroup, out int dirIndex);

        if (activeImage != null && activeGroup != null)
        {
            StartCoroutine(RunSequence(activeImage, activeGroup, trainWarningSprite, routeSprite, totalWarningDuration, onSpawn, dirIndex));
        }
        else
        {
            onSpawn?.Invoke();
        }
    }

    private void GetActiveReferences(TrainJunctionSpawner.TrackDirection direction, out Image img, out CanvasGroup group, out int dirIndex)
    {
        switch (direction)
        {
            case TrainJunctionSpawner.TrackDirection.Top:
                img = topTrackImage;
                group = topImageCanvasGroup;
                dirIndex = 0;
                break;
            case TrainJunctionSpawner.TrackDirection.Right:
                img = rightTrackImage;
                group = rightImageCanvasGroup;
                dirIndex = 1;
                break;
            case TrainJunctionSpawner.TrackDirection.Bottom:
                img = bottomTrackImage;
                group = bottomImageCanvasGroup;
                dirIndex = 2;
                break;
            case TrainJunctionSpawner.TrackDirection.Left:
                img = leftTrackImage;
                group = leftImageCanvasGroup;
                dirIndex = 3;
                break;
            default:
                img = null;
                group = null;
                dirIndex = -1;
                break;
        }
    }

    private IEnumerator RunSequence(Image activeImage, CanvasGroup activeGroup, Sprite trainWarningSprite, Sprite destinationRouteSprite, float totalWarningDuration, Action onSpawn, int dirIndex)
    {
        activeWarningsCount[dirIndex]++;
        
        float actualWarningSignDuration = Mathf.Min(warningSignDuration, totalWarningDuration * 0.2f);
        float actualRouteShowDuration = Mathf.Max(0f, totalWarningDuration - actualWarningSignDuration);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        activeImage.sprite = trainWarningSprite != null ? trainWarningSprite : warningSprite;
        activeGroup.alpha = 1f;

        if (audioSource != null && warningSfx != null)
        {
            audioSource.PlayOneShot(warningSfx);
        }

        yield return new WaitForSeconds(actualWarningSignDuration);

        activeImage.sprite = destinationRouteSprite;

        yield return new WaitForSeconds(actualRouteShowDuration);

        onSpawn?.Invoke();

        activeWarningsCount[dirIndex]--;

        if (activeWarningsCount[dirIndex] <= 0)
        {
            activeWarningsCount[dirIndex] = 0;
            activeGroup.alpha = 0f;
        }
    }

    private static void SetGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group != null) group.alpha = alpha;
    }
}
