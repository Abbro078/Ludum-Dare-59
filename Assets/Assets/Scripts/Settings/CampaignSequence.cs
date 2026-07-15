using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MainCampaign", menuName = "Game/Campaign Sequence")]
public class CampaignSequence : ScriptableObject
{
    [Tooltip("List of all days in order.")]
    public List<DaySettings> days;
}
