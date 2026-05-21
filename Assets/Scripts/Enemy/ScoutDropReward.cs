using UnityEngine;

public class ScoutDropReward : MonoBehaviour
{
    [SerializeField] private GameObject batteryPickupPrefab;
    private bool hasDropped;

    public void Setup()
    {
        // Zoek prefab of laad uit resources
        if (batteryPickupPrefab == null)
            batteryPickupPrefab = Resources.Load<GameObject>("Pickups/BatteryPickup");
    }

    private void OnDestroy()
    {
        if (!hasDropped && batteryPickupPrefab != null)
        {
            Instantiate(batteryPickupPrefab, transform.position, Quaternion.identity);
            hasDropped = true;
        }
    }
}