using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject buttonsPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings UI")]
    [SerializeField] private Toggle fullscreenToggle;

    [Header("HUD")]
    [SerializeField] private GameObject hudRoot;

    [Header("Player")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInput playerInput;

    private bool isPaused;

    private void Start()
    {
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (playerInput == null)
            playerInput = FindObjectOfType<PlayerInput>();

        if (fullscreenToggle != null)
        {
            bool fs = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            fullscreenToggle.SetIsOnWithoutNotify(fs);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (!isPaused && (playerInput == null || !playerInput.enabled)) return;

        if (isPaused) Resume();
        else Pause();
    }

    private void Pause()
    {
        isPaused = true;

        pausePanel.SetActive(true);
        buttonsPanel.SetActive(true);
        settingsPanel.SetActive(false);

        if (hudRoot != null) hudRoot.SetActive(false);
        if (playerController != null) playerController.SetControlsEnabled(false);
        if (playerInput != null) playerInput.enabled = false;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;

        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);

        if (hudRoot != null) hudRoot.SetActive(true);
        if (playerController != null) playerController.SetControlsEnabled(true);
        if (playerInput != null) playerInput.enabled = true;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OpenSettings()
    {
        buttonsPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void BackToMain()
    {
        settingsPanel.SetActive(false);
        buttonsPanel.SetActive(true);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnFullscreenChanged(bool value)
    {
        Screen.fullScreen = value;
        PlayerPrefs.SetInt("Fullscreen", value ? 1 : 0);
        PlayerPrefs.Save();
    }
}