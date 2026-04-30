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
            gameOverPanel.SetActive(false);

        FindReferencesIfNeeded();
    }

    private void OnEnable()
    {
        FindReferencesIfNeeded();

        if (healthController != null)
            healthController.OnDeath += ShowGameOver;
    }

    private void OnDisable()
    {
        if (healthController != null)
            healthController.OnDeath -= ShowGameOver;
    }

    private void FindReferencesIfNeeded()
    {
        GameObject player = PlayerFinder.FindPlayerObject();

        if (player == null)
            return;

        if (healthController == null)
            healthController = player.GetComponent<HealthController>();

        if (playerController == null)
            playerController = player.GetComponent<PlayerController>();

        if (playerInput == null)
            playerInput = player.GetComponent<PlayerInput>();
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (playerController != null)
            playerController.SetControlsEnabled(false);

        if (playerInput != null)
            playerInput.enabled = false;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}