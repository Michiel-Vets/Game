using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    [Header("Combo Settings")]
    [Tooltip("Aantal kills nodig om een combo te starten.")]
    [SerializeField] private int killsToActivate = 3;
    [Tooltip("Maximale tijd (s) tussen kills om als combo te tellen.")]
    [SerializeField] private float comboWindowSeconds = 2f;
    [Tooltip("Seconden zonder kill voordat de combo stopt.")]
    [SerializeField] private float comboIdleTimeout = 5f;
    [Tooltip("Seconden dat de combo-tekst fadet nadat de timeout is bereikt.")]
    [SerializeField] private float comboFadeDuration = 0.3f;
    [Tooltip("Batterij die terugkomt bij elke kill tijdens een combo.")]
    [SerializeField] private float batteryRechargePerKill = 8f;

    [Header("Overcharged Battery")]
    [Tooltip("Hoe veel sneller de batterij leegloopt tijdens een combo (8 = 8× zo snel).")]
    [SerializeField, Range(1f, 20f)] private float overchargedDrainMultiplier = 8f;
    [Tooltip("Extra damage-multiplier op geesten tijdens een actieve combo.")]
    [SerializeField, Range(1f, 5f)] private float comboDamageMultiplier = 2f;

    [Header("Overused")]
    [Tooltip("Kleur van de combo-tekst tijdens overused.")]
    [SerializeField] private Color overusedTextColor = new Color(1f, 0.1f, 0.05f);
    [Tooltip("Extra damage-multiplier op geesten tijdens overused (bovenop combo).")]
    [SerializeField, Range(1f, 8f)] private float overusedDamageMultiplier = 3.5f;

    [Header("UI")]
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private CanvasGroup comboCanvasGroup;

    [Header("References")]
    [SerializeField] private FlashlightController flashlightController;
    [SerializeField] private BatteryController batteryController;

    // ── Public API ───────────────────────────────────────────────────────────
    public bool IsComboActive  => _comboActive;
    public bool IsOverused     => _overused;

    /// <summary>Gebruikt door FlashlightController om de drain te verhogen tijdens een combo.</summary>
    public float OverchargedDrainMultiplier => overchargedDrainMultiplier;

    /// <summary>Extra flashlight-damage multiplier tijdens een actieve combo.</summary>
    public float ComboDamageMultiplier => _comboActive ? comboDamageMultiplier : 1f;

    /// <summary>Extra flashlight-damage multiplier tijdens overused.</summary>
    public float OverusedDamageMultiplier => _overused ? overusedDamageMultiplier : 1f;

    // ── State ────────────────────────────────────────────────────────────────
    private readonly Queue<float> _recentKills = new Queue<float>();
    private bool  _comboActive;
    private bool  _isFading;
    private bool  _overused;
    private float _timeSinceLastKill;
    private int   _comboCount;

    private Color _defaultTextColor;
    private OverusedEffect _overusedEffect;

    private void Awake()
    {
        Instance = this;
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 0f;
        if (comboText != null) _defaultTextColor = comboText.color;
        _overusedEffect = GetComponent<OverusedEffect>();
    }

    private void Update()
    {
        // ── Overused: check flashlight off → stop ────────────────────────────
        if (_overused)
        {
            if (flashlightController != null && !flashlightController.IsOn)
                StopOverused();
            return; // geen verdere combo-logica terwijl overused actief is
        }

        if (!_comboActive) return;

        _timeSinceLastKill += Time.deltaTime;

        // ── Overused trigger: batterij leeg tijdens actieve combo ────────────
        if (!_isFading && batteryController != null && !batteryController.HasBattery)
        {
            TriggerOverused();
            return;
        }

        // ── Idle timeout → fade → einde ──────────────────────────────────────
        if (!_isFading && _timeSinceLastKill >= comboIdleTimeout)
            _isFading = true;

        if (_isFading)
        {
            float fadeProgress = (_timeSinceLastKill - comboIdleTimeout) / Mathf.Max(comboFadeDuration, 0.01f);
            if (comboCanvasGroup != null)
                comboCanvasGroup.alpha = 1f - Mathf.Clamp01(fadeProgress);

            if (_timeSinceLastKill >= comboIdleTimeout + comboFadeDuration)
            {
                EndCombo();
                return;
            }
        }
        else
        {
            batteryController?.TickComboFlash(Time.deltaTime);
        }
    }

    // ── Kill events ──────────────────────────────────────────────────────────

    public void NotifyKill()
    {
        if (_overused) return;

        float now = Time.time;
        _recentKills.Enqueue(now);

        while (_recentKills.Count > 0 && now - _recentKills.Peek() > comboWindowSeconds)
            _recentKills.Dequeue();

        if (_comboActive)
        {
            _comboCount++;
            _timeSinceLastKill = 0f;
            _isFading = false;
            if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
            batteryController?.RechargeBattery(batteryRechargePerKill);
            RefreshText();
        }
        else if (_recentKills.Count >= killsToActivate)
        {
            StartCombo();
        }
    }

    // ── Internal state transitions ───────────────────────────────────────────

    private void StartCombo()
    {
        _comboActive = true;
        _isFading    = false;
        _overused    = false;
        _timeSinceLastKill = 0f;
        _comboCount  = _recentKills.Count;

        if (comboText != null)  comboText.color = _defaultTextColor;
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
        flashlightController?.SetComboBoosted(true);
        batteryController?.ResetBattery();   // overcharge: vul batterij naar 100%
        batteryController?.StartComboFlash();
        RefreshText();
    }

    private void EndCombo()
    {
        _comboActive = false;
        _isFading    = false;
        _comboCount  = 0;
        _recentKills.Clear();
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 0f;
        if (comboText != null) comboText.color = _defaultTextColor;
        flashlightController?.SetComboBoosted(false);
        batteryController?.StopComboFlash();
        batteryController?.EmptyBattery();   // overcharge verbruikt: batterij naar 0%
    }

    private void TriggerOverused()
    {
        _overused    = true;
        _comboActive = false;
        _isFading    = false;
        _comboCount  = 0;
        _recentKills.Clear();

        // Tekst rood + zichtbaar houden
        if (comboText != null)  comboText.color = overusedTextColor;
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
        RefreshOverusedText();

        flashlightController?.SetComboBoosted(false);
        batteryController?.StopComboFlash();
        batteryController?.StartOverused();
        _overusedEffect?.StartEffect();
    }

    private void StopOverused()
    {
        _overused = false;
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 0f;
        if (comboText != null) comboText.color = _defaultTextColor;
        batteryController?.StopOverused();
        _overusedEffect?.StopEffect();
        // Reset zodat een nieuwe combo kan starten
        _recentKills.Clear();
    }

    private void RefreshText()
    {
        if (comboText != null)
            comboText.text = $"{_comboCount}x COMBO!";
    }

    private void RefreshOverusedText()
    {
        if (comboText != null)
            comboText.text = "OVERUSED!";
    }
}
