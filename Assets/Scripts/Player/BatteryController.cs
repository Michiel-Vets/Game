using UnityEngine;
using UnityEngine.UI;

public class BatteryController : MonoBehaviour
{
    public static BatteryController Instance { get; private set; }
    [Header("Battery")]
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float currentBattery;
    [SerializeField] private float drainRate = 5f;

    [Header("Battery Bar")]
    [SerializeField] private RectTransform batteryFillTransform;
    [SerializeField] private Image batteryFillImage;
    [SerializeField] private Color fullColor = new Color(1f, 1f, 0f, 1f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    [Header("Combo")]
    [SerializeField] private Color comboColor   = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField] private Color overusedColor = new Color(1f, 0.2f, 0f, 1f);

    private bool _comboFlashing;
    private bool _overused;

    public float MaxBattery => maxBattery;
    public float CurrentBattery => currentBattery;
    public bool HasBattery => currentBattery > 0f;
    public float BatteryFraction => currentBattery / maxBattery;

    private float parentHeight;
    private float originalOffsetMinY;
    private float originalOffsetMaxY;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform parent = batteryFillTransform.parent as RectTransform;
        parentHeight = parent.rect.height;

        originalOffsetMinY = batteryFillTransform.offsetMin.y;
        originalOffsetMaxY = batteryFillTransform.offsetMax.y;

        batteryFillTransform.anchorMin = new Vector2(0f, 0f);
        batteryFillTransform.anchorMax = new Vector2(1f, 1f);

        maxBattery = Mathf.Max(1f, maxBattery);
        currentBattery = maxBattery;
        UpdateBatteryVisuals();
    }

    public void DrainBattery(float deltaTime)
    {
        if (currentBattery <= 0f) return;
        currentBattery = Mathf.Clamp(currentBattery - drainRate * deltaTime, 0f, maxBattery);
        UpdateBatteryVisuals();
    }

    public void RechargeBattery(float amount)
    {
        currentBattery = Mathf.Clamp(currentBattery + amount, 0f, maxBattery);
        UpdateBatteryVisuals();
    }

    public void ResetBattery()
    {
        currentBattery = maxBattery;
        UpdateBatteryVisuals();
    }

    public void EmptyBattery()
    {
        currentBattery = 0f;
        UpdateBatteryVisuals();
    }

    public void StartComboFlash()
    {
        _comboFlashing = true;
        if (batteryFillImage != null)
            batteryFillImage.color = comboColor;
    }

    public void StopComboFlash()
    {
        _comboFlashing = false;
        if (!_overused) UpdateBatteryVisuals();
    }

    public void StartOverused()
    {
        _overused = true;
        if (batteryFillImage != null)
            batteryFillImage.color = overusedColor;
    }

    public void StopOverused()
    {
        _overused = false;
        UpdateBatteryVisuals();
    }

    private void UpdateBatteryVisuals()
    {
        if (batteryFillTransform != null)
        {
            Vector2 offsetMax = batteryFillTransform.offsetMax;
            offsetMax.y = Mathf.Lerp(-(parentHeight - originalOffsetMinY), originalOffsetMaxY, BatteryFraction);
            batteryFillTransform.offsetMax = offsetMax;
        }

        if (batteryFillImage != null && !_comboFlashing)
            batteryFillImage.color = Color.Lerp(lowColor, fullColor, BatteryFraction);
    }
}