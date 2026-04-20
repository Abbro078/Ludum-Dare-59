using UnityEngine;

public class TrainCart : MonoBehaviour
{
    [Tooltip("Link this to the main TrainController managing this train.")]
    public TrainController mainController;

    private void OnTriggerEnter(Collider other)
    {
        if (mainController != null) mainController.HandleCollision(other);
    }

    private void OnTriggerExit(Collider other)
    {
        // Tell the main controller a cart has left the trigger!
        if (mainController != null) mainController.HandleTriggerExit(other);
    }
}