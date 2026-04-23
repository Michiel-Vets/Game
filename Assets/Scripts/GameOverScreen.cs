using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private HealthController healthController;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInput playerInput;

    private void Awake()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (healthController != null)
        {
            healthController.OnDeath += ShowGameOver;
        }
    }

    private void OnDisable()
    {
        if (healthController != null)
        {
            healthController.OnDeath -= ShowGameOver;
        }
    }

    private void ShowGameOver()
    {
        // Ik toon het game over scherm wanneer de speler sterft.
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (playerController != null)
        {
            playerController.SetControlsEnabled(false);
        }

        if (playerInput != null)
        {
            playerInput.enabled = false;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Restart()
    {
        // Ik laad de huidige scene opnieuw zodat alles volledig reset.
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void Quit()
    {
        // Ik sluit de game af.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}