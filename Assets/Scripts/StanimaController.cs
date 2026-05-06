using UnityEngine;

public class StaminaController : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrainRate = 1f;
    [SerializeField] private float staminaRechargeRate = 0.5f;
    [Tooltip("Minimale fractie die hersteld moet zijn voordat sprinten opnieuw mogelijk is.")]
    [SerializeField, Range(0f, 1f)] private float rechargeThreshold = 0.25f;

    [Header("Overexhausted (stamina leeg maar shift ingedrukt)")]
    [Tooltip("Snelheidsmultiplier tijdens overexhaustion (tussen loop- en sprintsnelheid).")]
    [SerializeField, Range(0f, 1f)] private float overexhaustedSpeedFraction = 0.6f;

    [Header("Stamina Bar")]
    [SerializeField] private RectTransform staminaBar;
    [SerializeField] private float fullWidth = 200f;
    [SerializeField] private float barHeight = 20f;

    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    public float StaminaFraction => maxStamina > 0f ? currentStamina / maxStamina : 0f;
    public bool IsExhausted => isExhausted;

    /// <summary>
    /// True als stamina leeg is én de speler shift nog ingedrukt houdt.
    /// In deze toestand beweegt de speler op overexhaustedSpeedFraction en laadt stamina niet op.
    /// </summary>
    public bool IsOverexhausted => isExhausted && isShiftHeld;

    public float OverexhaustedSpeedFraction => overexhaustedSpeedFraction;

    private float currentStamina;
    private bool isExhausted;
    private bool isShiftHeld;

    private void Awake()
    {
        maxStamina = Mathf.Max(0.1f, maxStamina);
        currentStamina = maxStamina;

        SetupStaminaBar();
        UpdateStaminaVisuals();
    }

    /// <summary>
    /// Moet elke frame aangeroepen worden vanuit PlayerController
    /// zodat StaminaController weet of shift ingedrukt is.
    /// </summary>
    public void SetShiftHeld(bool held)
    {
        isShiftHeld = held;
    }

    /// <summary>
    /// Verbruikt stamina tijdens het sprinten. Geeft true terug als er nog stamina over is.
    /// </summary>
    public bool DrainStamina(float deltaTime)
    {
        if (isExhausted)
            return false;

        currentStamina -= staminaDrainRate * deltaTime;

        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            isExhausted = true;
        }

        UpdateStaminaVisuals();
        return !isExhausted;
    }

    /// <summary>
    /// Herstelt stamina — alleen als shift NIET ingedrukt is.
    /// </summary>
    public void RechargeStamina(float deltaTime)
    {
        // Stamina laadt niet op zolang shift ingedrukt is na uitputting
        if (isShiftHeld && isExhausted)
            return;

        if (currentStamina >= maxStamina)
            return;

        currentStamina = Mathf.Min(currentStamina + staminaRechargeRate * deltaTime, maxStamina);

        if (isExhausted && currentStamina >= maxStamina * rechargeThreshold)
            isExhausted = false;

        UpdateStaminaVisuals();
    }

    public void ResetStamina()
    {
        currentStamina = maxStamina;
        isExhausted = false;
        UpdateStaminaVisuals();
    }

    private void SetupStaminaBar()
    {
        if (staminaBar == null)
            return;

        staminaBar.pivot = new Vector2(0f, 0.5f);
        staminaBar.anchorMin = new Vector2(0f, 0.5f);
        staminaBar.anchorMax = new Vector2(0f, 0.5f);
    }

    private void UpdateStaminaVisuals()
    {
        if (staminaBar == null)
            return;

        staminaBar.sizeDelta = new Vector2(fullWidth * StaminaFraction, barHeight);
    }
}