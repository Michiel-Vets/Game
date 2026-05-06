using UnityEngine;

public class BatteryController : MonoBehaviour
{
    [Header("Battery")]
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float currentBattery;
    [SerializeField] private float drainRate = 5f;

    [Header("Battery Bar")]
    [SerializeField] private RectTransform batteryBar;
    [SerializeField] private float fullHeight = 200f;
    [SerializeField] private float barWidth = 20f;

    public float MaxBattery => maxBattery;
    public float CurrentBattery => currentBattery;
    public bool HasBattery => currentBattery > 0f;
    public float BatteryFraction => currentBattery / maxBattery;

    private void Awake()
    {
        maxBattery = Mathf.Max(1f, maxBattery);
        currentBattery = maxBattery;

        SetupBatteryBar();
        UpdateBatteryVisuals();
    }

    public void DrainBattery(float deltaTime)
    {
        if (currentBattery <= 0f)
            return;

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

    private void SetupBatteryBar()
    {
        if (batteryBar == null)
            return;

        // Pivot onderaan zetten
        batteryBar.pivot = new Vector2(0.5f, 0f);

        // Anchors ook onderaan
        batteryBar.anchorMin = new Vector2(0.5f, 0f);
        batteryBar.anchorMax = new Vector2(0.5f, 0f);
    }

    private void UpdateBatteryVisuals()
    {
        if (batteryBar == null)
            return;

        batteryBar.sizeDelta = new Vector2(barWidth, fullHeight * BatteryFraction);
    }
}