using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaveProgressUI : MonoBehaviour
{
    public static WaveProgressUI Instance { get; private set; }

    [Header("Wave Progress Bar")]
    [SerializeField] private Image progressFill;
    [SerializeField] private GameObject progressContainer;

    [Header("Break Countdown")]
    [SerializeField] private TMP_Text breakCountdownText;
    [SerializeField] private GameObject breakCountdownContainer;

    [Header("Power-up Count")]
    [SerializeField] private TMP_Text powerUpCountText;
    [SerializeField] private GameObject powerUpCountContainer;

    [Header("Power-up Fail")]
    [SerializeField] private TMP_Text powerUpFailText;
    [SerializeField] private GameObject powerUpFailContainer;
    [SerializeField] private float powerUpFailDisplayDuration = 5f;

    private float _failTextTimer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (progressContainer != null)
            progressContainer.SetActive(false);
        SetBreakCountdownVisible(false);
        SetActive(powerUpCountContainer, powerUpCountText, false);
        SetActive(powerUpFailContainer,  powerUpFailText,  false);
    }

    private void Update()
    {
        if (_failTextTimer > 0f)
        {
            _failTextTimer -= Time.deltaTime;
            if (_failTextTimer <= 0f)
                SetActive(powerUpFailContainer, powerUpFailText, false);
        }
    }

    // ── Wave progress ─────────────────────────────────────────────────────────

    public void SetProgress(float progress)
    {
        if (progressContainer != null && !progressContainer.activeSelf)
            progressContainer.SetActive(true);

        if (progressFill != null)
            progressFill.fillAmount = progress;

        if (progress >= 0.99f)
            Hide();
    }

    public void Hide()
    {
        if (progressContainer != null)
            progressContainer.SetActive(false);
    }

    // ── Break countdown ───────────────────────────────────────────────────────

    public void ShowBreakCountdown(float secondsRemaining, bool isFirstWave = false)
    {
        SetBreakCountdownVisible(true);
        if (breakCountdownText != null)
        {
            int s = Mathf.CeilToInt(secondsRemaining);
            if (isFirstWave)
                breakCountdownText.text = s > 0 ? $"Game starts in {s}s" : "Wave starting...";
            else
                breakCountdownText.text = s > 0 ? $"Next wave in {s}s" : "Wave starting...";
        }
    }

    public void HideBreakCountdown()
    {
        SetBreakCountdownVisible(false);
    }

    private void SetBreakCountdownVisible(bool visible)
    {
        GameObject root = breakCountdownContainer != null ? breakCountdownContainer
                        : breakCountdownText != null ? breakCountdownText.gameObject
                        : null;
        if (root != null) root.SetActive(visible);
    }

    // ── Power-up count ────────────────────────────────────────────────────────

    public void ShowPowerUpCount(int collected, int total)
    {
        SetActive(powerUpCountContainer, powerUpCountText, true);
        if (powerUpCountText != null)
            powerUpCountText.text = $"{collected}/{total} power-ups";
    }

    public void HidePowerUpCount()
    {
        SetActive(powerUpCountContainer, powerUpCountText, false);
    }

    // ── Power-up fail text ────────────────────────────────────────────────────

    public void ShowPowerUpFailText(float growthModifier)
    {
        string msg = growthModifier < 0.01f
            ? "No power-ups collected!\nThe map won't grow."
            : $"Only {Mathf.RoundToInt(growthModifier * 100f)}% power-ups collected!\nThe map grows less.";

        if (powerUpFailText != null) powerUpFailText.text = msg;
        SetActive(powerUpFailContainer, powerUpFailText, true);
        _failTextTimer = powerUpFailDisplayDuration;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static void SetActive(GameObject container, TMP_Text text, bool active)
    {
        if (container != null)
            container.SetActive(active);
        else if (text != null)
            text.gameObject.SetActive(active);
    }
}
