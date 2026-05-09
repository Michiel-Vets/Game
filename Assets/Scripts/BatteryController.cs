using UnityEngine;
using UnityEngine.UI;

public class BatteryController : MonoBehaviour
{
    [Header("Battery")]
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float currentBattery;
    [SerializeField] private float drainRate = 5f;

    [Header("Battery Bar")]
    [SerializeField] private RectTransform batteryFillTransform;
    [SerializeField] private Image batteryFillImage;
    [SerializeField] private Color fullColor = new Color(0.24f, 0.72f, 0.24f, 1f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    public float MaxBattery => maxBattery;
    public float CurrentBattery => currentBattery;
    public bool HasBattery => currentBattery > 0f;
    public float BatteryFraction => currentBattery / maxBattery;

    private float parentHeight;
    private float originalOffsetMinY;
    private float originalOffsetMaxY;

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

    private void UpdateBatteryVisuals()
    {
        if (batteryFillTransform != null)
        {
            Vector2 offsetMax = batteryFillTransform.offsetMax;
            offsetMax.y = Mathf.Lerp(-(parentHeight - originalOffsetMinY), originalOffsetMaxY, BatteryFraction);
            batteryFillTransform.offsetMax = offsetMax;
        }

        if (batteryFillImage != null)
            batteryFillImage.color = Color.Lerp(lowColor, fullColor, BatteryFraction);
    }
}