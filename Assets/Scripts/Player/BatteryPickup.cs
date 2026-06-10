using UnityEngine;

public class BatteryPickup : BasePickup
{
    [SerializeField] private float rechargeAmount = 50f;

    private BatteryController _battery;

    protected override void Start()
    {
        base.Start();
        _battery = FindObjectOfType<BatteryController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        if (_battery == null || _battery.BatteryFraction >= 1f) return;
        if (ComboSystem.Instance != null && (ComboSystem.Instance.IsComboActive || ComboSystem.Instance.IsOverused)) return;

        _battery.RechargeBattery(rechargeAmount);
        Destroy(gameObject);
    }
}
