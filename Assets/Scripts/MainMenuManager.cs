using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Routing")]
    [Tooltip("The exact name of the Game Scene to load. Make sure this scene is added to your Build Settings!")]
    public string gameSceneName = "GameScene";

    /// <summary>
    /// Loads the main game scene. Link this to your 'Play' button's OnClick event.
    /// </summary>
    public void PlayGame()
    {
        // Reset time and audio states perfectly just in case the player quit while paused
        Time.timeScale = 1f;
        AudioListener.pause = false; 

        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Quits the application. Link this to your 'Quit' button's OnClick event.
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}
