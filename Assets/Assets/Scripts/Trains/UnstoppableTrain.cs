using UnityEngine;

public class UnstoppableTrain : TrainBase
{
    protected override void StopTrainCarts()
    {
        Debug.Log($"Unstoppable Train {gameObject.name} smashed into a closed gate!");
        
        PlayCrashSound();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }
}
