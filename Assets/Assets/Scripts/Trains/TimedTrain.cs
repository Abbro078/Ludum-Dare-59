using UnityEngine;

public class TimedTrain : TrainBase
{
    [Header("Timed Train Settings")]
    [Tooltip("How long the gate has before it is forced open by this train.")]
    public float forceOpenDuration = 3f;

    protected override void OnStoppedAtGate(CrossingGate gate)
    {
        base.OnStoppedAtGate(gate);
        
        if (gate != null)
        {
            gate.StartForceOpenTimer(forceOpenDuration);
        }
    }

    protected override void OnResumedFromGate(CrossingGate gate)
    {
        base.OnResumedFromGate(gate);

        if (gate != null)
        {
            gate.StopForceOpenTimer();
        }
    }
}
