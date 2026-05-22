using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaveProgressUI : MonoBehaviour
{
    [Header("Wave Progress Bar")]
    [SerializeField] private Image progressFill;
    [SerializeField] private GameObject progressContainer;

    [Header("Break Countdown")]
    [Tooltip("Optioneel TMP_Text voor de afteltimer tijdens een break.")]
    [SerializeField] private TMP_Text breakCountdownText;
    [SerializeField] private GameObject breakCountdownContainer;

    private void Start()
    {
        if (progressContainer != null)
            progressContainer.SetActive(false);
        SetBreakCountdownVisible(false);
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

    public void ShowBreakCountdown(float secondsRemaining)
    {
        SetBreakCountdownVisible(true);
        if (breakCountdownText != null)
        {
            int s = Mathf.CeilToInt(secondsRemaining);
            breakCountdownText.text = s > 0 ? $"Volgende wave over {s}s" : "Wave begint...";
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
}
