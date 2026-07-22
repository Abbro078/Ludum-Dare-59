using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Text element to display the current score.")]
    public TextMeshProUGUI scoreText;

    [Tooltip("Panel to show when the game is over.")]
    public GameObject gameOverPanel;

    [Tooltip("Text element ON the Game Over panel to display final score.")]
    public TextMeshProUGUI gameOverScoreText;

    [Tooltip("Panel to show when the game is paused.")]
    public GameObject pausePanel;

    [Tooltip("Panel to show when the day is complete.")]
    public GameObject dayCompletePanel;

    [Header("Scene Routing")]
    [Tooltip("The exact name of your Main Menu scene.")]
    public string mainMenuSceneName = "MainMenu";

    public int Score { get; private set; } = 0;
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        UpdateScoreUI();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (dayCompletePanel != null) dayCompletePanel.SetActive(false);
    }

    public void AddPoint()
    {
        if (isGameOver) return;

        Score++;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + Score;
        }
    }

    public void GameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (gameOverScoreText != null)
        {
            gameOverScoreText.text = Score.ToString();
        }

        Time.timeScale = 0f;
        AudioListener.pause = true; 

        Debug.Log("Game Over!");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void PauseGame()
    {
        if (isGameOver) return;
        
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    public void ResumeGame()
    {
        if (isGameOver) return;

        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ShowDayCompleteUI(int finalScore)
    {
        if (isGameOver) return;

        if (dayCompletePanel != null)
        {
            dayCompletePanel.SetActive(true);
        }

        Time.timeScale = 0f;
        AudioListener.pause = true; 
    }

    public void HideDayCompleteUI()
    {
        if (dayCompletePanel != null)
        {
            dayCompletePanel.SetActive(false);
        }

        Time.timeScale = 1f;
        AudioListener.pause = false; 
    }
}
