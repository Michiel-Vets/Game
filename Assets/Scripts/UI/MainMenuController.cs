using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings UI")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Button easyButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button hardButton;

    [Header("Scene")]
    [SerializeField] private int gameSceneIndex = 1;

    private readonly Color selectedColor = new Color(0.25f, 0.80f, 0.35f);
    private readonly Color defaultColor = Color.white;

    private void Start()
    {
        DifficultySettings.Load();

        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = fullscreen;
        fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        easyButton.onClick.AddListener(() => SetDifficulty(DifficultyLevel.Easy));
        normalButton.onClick.AddListener(() => SetDifficulty(DifficultyLevel.Normal));
        hardButton.onClick.AddListener(() => SetDifficulty(DifficultyLevel.Hard));

        ShowMain();
        RefreshDifficultyVisuals();
    }

    public void StartGame() => SceneManager.LoadScene(gameSceneIndex);

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void BackToMain()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowMain()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    private void OnFullscreenChanged(bool value)
    {
        Screen.fullScreen = value;
        PlayerPrefs.SetInt("Fullscreen", value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetDifficulty(DifficultyLevel level)
    {
        DifficultySettings.Current = level;
        DifficultySettings.Save();
        RefreshDifficultyVisuals();
    }

    private void RefreshDifficultyVisuals()
    {
        easyButton.image.color   = DifficultySettings.Current == DifficultyLevel.Easy   ? selectedColor : defaultColor;
        normalButton.image.color = DifficultySettings.Current == DifficultyLevel.Normal ? selectedColor : defaultColor;
        hardButton.image.color   = DifficultySettings.Current == DifficultyLevel.Hard   ? selectedColor : defaultColor;
    }
}
