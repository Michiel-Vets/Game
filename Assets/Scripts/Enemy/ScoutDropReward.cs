using UnityEngine;

public class ScoutDropReward : BaseDropReward
{
    [SerializeField] private GameObject batteryPickupPrefab;

    [Header("Drop Settings")]
    [Tooltip("Kans (0–1) dat de scout een batterij dropt.")]
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.20f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRaycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 0.3f;

    public void Setup()
    {
        if (batteryPickupPrefab == null)
            batteryPickupPrefab = Resources.Load<GameObject>("Pickups/BatteryPickup");

        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground");
    }

    protected override void SpawnReward()
    {
        if (batteryPickupPrefab == null || Random.value > dropChance) return;

        Vector3 rayOrigin = new Vector3(transform.position.x, transform.position.y + groundRaycastHeight, transform.position.z);
        Vector3 spawnPos  = new Vector3(
            transform.position.x,
            MapController.Instance != null ? MapController.Instance.SurfaceY + spawnYOffset : spawnYOffset,
            transform.position.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                groundRaycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point + Vector3.up * spawnYOffset;

        Instantiate(batteryPickupPrefab, spawnPos, Quaternion.identity);
    }
}
