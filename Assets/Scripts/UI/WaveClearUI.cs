using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toont tijdens een wave hoeveel enemies er gedood zijn (de "clear bar").
/// Gebruikt een CanvasGroup op hetzelfde object — geen apart container-object nodig.
/// Voeg een CanvasGroup component toe aan het WaveClearManager object in Unity.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class WaveClearUI : MonoBehaviour
{
    [Header("Progress Bar")]
    [SerializeField] private Image fillBar;
    [SerializeField] private Color clearedColor  = new Color(0.25f, 0.85f, 0.35f);
    [SerializeField] private Color progressColor = new Color(0.9f,  0.75f, 0.15f);
    [SerializeField] private Color dangerColor   = new Color(0.9f,  0.25f, 0.15f);

    [Header("Labels")]
    [SerializeField] private TMP_Text killCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text siegeKillText;

    [Header("Timing")]
    [SerializeField] private float messageFadeDuration = 0.6f;
    [SerializeField] private float messageHoldDuration = 3.0f;

    private CanvasGroup _group;
    private Coroutine   _messageCoroutine;
    private bool        _isSiegeWave;
    private bool        _waveActive;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        SetVisible(false);
    }

    // ── Wave lifecycle ────────────────────────────────────────────────────────

    public void OnWaveStarted(bool isSiege)
    {
        _isSiegeWave = isSiege;
        _waveActive  = true;
        SetVisible(true);

        if (statusText    != null) { statusText.text    = ""; statusText.alpha = 1f; }
        if (fillBar       != null) { fillBar.fillAmount = 0f; fillBar.color    = progressColor; }
        if (killCountText != null) killCountText.text   = isSiege ? "" : "0 / 0";

        if (siegeKillText != null) siegeKillText.gameObject.SetActive(isSiege);
        if (killCountText != null) killCountText.gameObject.SetActive(!isSiege);
        if (fillBar       != null) fillBar.gameObject.SetActive(!isSiege);
    }

    public void OnWaveEnded()
    {
        _waveActive = false;
        if (_messageCoroutine == null)
            SetVisible(false);
    }

    // ── Progress ──────────────────────────────────────────────────────────────

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
            fillBar.color = fraction >= 1f   ? clearedColor
                          : fraction >= 0.6f ? progressColor
                          : dangerColor;
        }

        if (killCountText != null)
            killCountText.text = $"{killed} / {total}";
    }

    // ── End-of-wave feedback ──────────────────────────────────────────────────

    public void ShowWaveCleared()
    {
        ShowMessage("WAVE CLEARED!", clearedColor);
    }

    public void ShowPenalty(float penaltyMultiplier)
    {
        if (penaltyMultiplier <= 1.01f) { SetVisible(false); return; }
        int pct = Mathf.RoundToInt((penaltyMultiplier - 1f) * 100f);
        ShowMessage($"NOT CLEARED  +{pct}% HARDER", dangerColor);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetVisible(bool visible)
    {
        if (_group == null) return;
        _group.alpha          = visible ? 1f : 0f;
        _group.blocksRaycasts = visible;
    }

    private void ShowMessage(string message, Color color)
    {
        if (_messageCoroutine != null) StopCoroutine(_messageCoroutine);
        _messageCoroutine = StartCoroutine(MessageCoroutine(message, color));
    }

    private IEnumerator MessageCoroutine(string message, Color color)
    {
        SetVisible(true);

        if (statusText != null)
        {
            statusText.text  = message;
            statusText.color = color;
            statusText.alpha = 1f;
        }

        float hold = messageHoldDuration - messageFadeDuration;
        if (hold > 0f) yield return new WaitForSeconds(hold);

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
        // Verberg de balk alleen als de wave al voorbij is
        if (!_waveActive)
            SetVisible(false);
    }
}
