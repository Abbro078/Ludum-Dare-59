using UnityEngine;

public class ClickableDebris : DebrisBase
{
    [Header("Click Settings")]
    [Tooltip("How many clicks it takes to completely clear this debris from the tracks.")]
    public int requiredClicks = 3;

    private int currentClicks = 0;

    protected override void OnClicked()
    {
        currentClicks++;
        
        if (currentClicks >= requiredClicks)
        {
            TriggerDestruction();
        }
        else
        {
            PlayClickSound();
            
            // Optional: You could add a visual squish/shake effect here!
            // transform.localScale = Vector3.one * 0.9f; 
        }
    }

    public void SetRequiredClicks(int clicks)
    {
        if (clicks > 0)
        {
            requiredClicks = clicks;
        }
    }
}
