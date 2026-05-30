using UnityEngine;

public class ScoutDropReward : MonoBehaviour
{
    [SerializeField] private GameObject batteryPickupPrefab;

    [Header("Drop Settings")]
    [Tooltip("Kans (0–1) dat de scout een batterij dropt.")]
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.20f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRaycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 0.3f;

    private bool _hasDropped;
    private static bool _appQuitting;

    private void OnApplicationQuit() => _appQuitting = true;

    /// <summary>Voorkomt dat de batterij dropt (aanroepen vóór Destroy bij despawn).</summary>
    public void CancelDrop() => _hasDropped = true;

    public void Setup()
    {
        if (batteryPickupPrefab == null)
            batteryPickupPrefab = Resources.Load<GameObject>("Pickups/BatteryPickup");

        // Zorg dat de groundLayer standaard de "Ground" laag bevat als de inspector leeg is
        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground");
    }

    private void OnDestroy()
    {
        if (_hasDropped || batteryPickupPrefab == null || _appQuitting || !Application.isPlaying) return;
        if (Random.value > dropChance) return;

        _hasDropped = true;

        // Vind de grond onder de scout zodat de pickup niet in de lucht hangt
        Vector3 rayOrigin = new Vector3(transform.position.x, transform.position.y + groundRaycastHeight, transform.position.z);
        Vector3 spawnPos = new Vector3(transform.position.x,
            MapController.Instance != null ? MapController.Instance.SurfaceY + spawnYOffset : spawnYOffset,
            transform.position.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                rayOrigin.y + groundRaycastHeight, groundLayer, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point + Vector3.up * spawnYOffset;

        Instantiate(batteryPickupPrefab, spawnPos, Quaternion.identity);
    }
}