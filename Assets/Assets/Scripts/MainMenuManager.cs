using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Routing")]
    [Tooltip("The exact name of the Game Scene to load. Make sure this scene is added to your Build Settings!")]
    public string gameSceneName = "GameScene";
    
    public void PlayGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false; 

        SceneManager.LoadScene(gameSceneName);
    }
    
    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}
