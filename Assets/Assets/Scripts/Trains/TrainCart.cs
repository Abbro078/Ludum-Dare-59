using UnityEngine;

public class TrainCart : MonoBehaviour
{
    [Tooltip("Link this to the main TrainBase managing this train.")]
    public TrainBase mainController;

    private void OnTriggerEnter(Collider other)
    {
        if (mainController != null) mainController.HandleCollision(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (mainController != null) mainController.HandleTriggerExit(other);
    }
}
