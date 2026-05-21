using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toont tijdens een wave hoeveel enemies er gedood zijn (de "clear bar").
/// Geeft aan het einde van een wave feedback of de wave gecleared is,
/// en hoeveel de strafmultiplier is als dat niet zo is.
/// </summary>
public class WaveClearUI : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private GameObject container;

    [Header("Progress Bar")]
    [SerializeField] private Image fillBar;
    [SerializeField] private Color clearedColor   = new Color(0.25f, 0.85f, 0.35f);
    [SerializeField] private Color progressColor  = new Color(0.9f,  0.75f, 0.15f);
    [SerializeField] private Color dangerColor    = new Color(0.9f,  0.25f, 0.15f);

    [Header("Labels")]
    [SerializeField] private TMP_Text killCountText;   // bijv. "7 / 12"
    [SerializeField] private TMP_Text statusText;      // "WAVE CLEARED!" / "NOT CLEARED  +30%"
    [SerializeField] private TMP_Text siegeKillText;   // alleen zichtbaar tijdens Siege

    [Header("Timing")]
    [SerializeField] private float messageFadeDuration = 0.6f;
    [SerializeField] private float messageHoldDuration = 3.0f;

    private Coroutine _messageCoroutine;
    private bool _isSiegeWave;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Start()
    {
        SetContainerActive(false);
    }

    // ── Wave lifecycle hooks ─────────────────────────────────────────────────

    /// <summary>Aanroepen zodra een wave begint.</summary>
    public void OnWaveStarted(bool isSiege)
    {
        _isSiegeWave = isSiege;
        SetContainerActive(true);

        if (statusText  != null) { statusText.text  = ""; statusText.alpha  = 1f; }
        if (fillBar     != null) { fillBar.fillAmount = 0f; fillBar.color    = progressColor; }
        if (killCountText != null) killCountText.text = isSiege ? "∞" : "0 / 0";

        // Siege waves tonen het kill-tekst veld anders
        if (siegeKillText != null) siegeKillText.gameObject.SetActive(isSiege);
        if (killCountText != null) killCountText.gameObject.SetActive(!isSiege);
        if (fillBar       != null) fillBar.gameObject.SetActive(!isSiege);
    }

    /// <summary>Aanroepen op het einde van een wave (pauze begint).</summary>
    public void OnWaveEnded()
    {
        // Verberg de bar na het tonen van het resultaat (coroutine doet dit later)
        // Verberg nu alleen als er geen status bericht speelt
        if (_messageCoroutine == null)
            SetContainerActive(false);
    }

    // ── Progress updates ─────────────────────────────────────────────────────

    /// <summary>Bijwerken na elke kill of elke spawn.</summary>
    public void UpdateProgress(int killed, int total)
    {
        if (_isSiegeWave)
        {
            if (siegeKillText != null)
                siegeKillText.text = $"Kills: {killed}";
            return;
        }

        float fraction = total > 0 ? Mathf.Clamp01((float)killed / total) : 0f;

        if (fillBar != null)
        {
            fillBar.fillAmount = fraction;
            fillBar.color = fraction >= 1f ? clearedColor
                          : fraction >= 0.6f ? progressColor
                          : dangerColor;
        }

        if (killCountText != null)
            killCountText.text = $"{killed} / {total}";
    }

    // ── End-of-wave feedback ─────────────────────────────────────────────────

    /// <summary>Toon "WAVE CLEARED!" melding.</summary>
    public void ShowWaveCleared()
    {
        ShowMessage("WAVE CLEARED!", clearedColor);
    }

    /// <summary>Toon strafmelding als de wave niet gecleared is.</summary>
    public void ShowPenalty(float penaltyMultiplier)
    {
        if (penaltyMultiplier <= 1.01f)
        {
            SetContainerActive(false);
            return;
        }

        int pct = Mathf.RoundToInt((penaltyMultiplier - 1f) * 100f);
        ShowMessage($"NOT CLEARED  +{pct}% HARDER", dangerColor);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ShowMessage(string message, Color color)
    {
        if (_messageCoroutine != null) StopCoroutine(_messageCoroutine);
        _messageCoroutine = StartCoroutine(MessageCoroutine(message, color));
    }

    private IEnumerator MessageCoroutine(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text  = message;
            statusText.color = color;
            statusText.alpha = 1f;
        }

        // Houd de boodschap vast
        float hold = messageHoldDuration - messageFadeDuration;
        if (hold > 0f) yield return new WaitForSeconds(hold);

        // Fade uit
        float t = 0f;
        while (t < messageFadeDuration)
        {
            if (statusText != null)
                statusText.alpha = 1f - t / messageFadeDuration;
            t += Time.deltaTime;
            yield return null;
        }

        if (statusText != null) statusText.alpha = 0f;
        _messageCoroutine = null;
        SetContainerActive(false);
    }

    private void SetContainerActive(bool active)
    {
        if (container != null) container.SetActive(active);
    }
}
