using UnityEngine;
using UnityEngine.UI;

public class StaminaController : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrainRate = 1f;
    [SerializeField] private float staminaRechargeRate = 0.5f;
    [Tooltip("Minimale fractie die hersteld moet zijn voordat sprinten opnieuw mogelijk is.")]
    [SerializeField, Range(0f, 1f)] private float rechargeThreshold = 0.25f;
    [SerializeField] private float jumpStaminaCost = 1.5f;

    [Header("Overexhausted (stamina leeg maar shift ingedrukt)")]
    [Tooltip("Snelheidsmultiplier tijdens overexhaustion (tussen loop- en sprintsnelheid).")]
    [SerializeField, Range(0f, 1f)] private float overexhaustedSpeedFraction = 0.6f;

    [Header("Stamina Bar")]
    [SerializeField] private RectTransform staminaBar;
    [SerializeField] private Image staminaBarBackground;

    [Header("Overexhausted Flash")]
    [SerializeField] private float flashSpeed = 4f;
    [SerializeField] private Color flashColorA = Color.gray;
    [SerializeField] private Color flashColorB = new Color(0.6f, 0f, 0f, 1f);

    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    public float StaminaFraction => maxStamina > 0f ? currentStamina / maxStamina : 0f;
    public bool IsExhausted => isExhausted;

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

    private void Update()
    {
        UpdateBackgroundFlash();
    }

    public void SetShiftHeld(bool held)
    {
        isShiftHeld = held;
    }

    public void DrainStaminaForJump()
    {
        if (isExhausted)
            return;

        currentStamina = Mathf.Max(0f, currentStamina - jumpStaminaCost);

        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            isExhausted = true;
        }

        UpdateStaminaVisuals();
    }

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

    public void RechargeStamina(float deltaTime)
    {
        if (currentStamina >= maxStamina)
            return;

        currentStamina = Mathf.Min(currentStamina + staminaRechargeRate * deltaTime, maxStamina);

        // Exhaustion opheft zodra de drempel bereikt is, ongeacht of shift ingedrukt is.
        // Sprinten zelf is al geblokkeerd in PlayerController zolang IsExhausted true is.
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

        // Pivot en anchors links: de balk krimpt vanuit rechts, links blijft als laatste over
        staminaBar.pivot = new Vector2(0f, 0.5f);
        staminaBar.anchorMin = new Vector2(0f, 0.5f);
        staminaBar.anchorMax = new Vector2(0f, 0.5f);
    }

    private void UpdateStaminaVisuals()
    {
        if (staminaBar == null)
            return;

        staminaBar.localScale = new Vector3(StaminaFraction, 1f, 1f);
    }

    private void UpdateBackgroundFlash()
    {
        if (staminaBarBackground == null)
            return;

        // Flash alleen als de speler overexhausted is (stamina leeg én shift ingedrukt)
        if (IsOverexhausted)
        {
            float t = (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f;
            staminaBarBackground.color = Color.Lerp(flashColorA, flashColorB, t);
        }
        else
        {
            staminaBarBackground.color = flashColorA;
        }
    }
}