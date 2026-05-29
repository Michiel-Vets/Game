using UnityEngine;

/// <summary>
/// Voeg dit toe aan een scout-geest die een power-up draagt.
/// Spawn de power-up wanneer de geest wordt vernietigd.
/// </summary>
public class PowerUpDropReward : MonoBehaviour
{
    private GameObject _powerUpPrefab;
    private bool _dropped;
    private static bool _appQuitting;

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRaycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 0.5f;

    private void OnApplicationQuit() => _appQuitting = true;

    public void Setup(GameObject powerUpPrefab)
    {
        _powerUpPrefab = powerUpPrefab;

        if (_powerUpPrefab == null)
            _powerUpPrefab = Resources.Load<GameObject>("Pickups/PowerUpPickup");

        if (_powerUpPrefab == null)
            Debug.LogError("PowerUpDropReward: geen power-up prefab gevonden. " +
                "Wijs 'Power Up Prefab' toe op PowerUpSpawner in de Inspector.");

        if (groundLayer == 0)
            groundLayer = LayerMask.GetMask("Ground");
    }

    private void OnDestroy()
    {
        if (_dropped || _powerUpPrefab == null || _appQuitting || !Application.isPlaying) return;
        _dropped = true;

        Vector3 spawnPos = transform.position;
        Vector3 rayOrigin = spawnPos + Vector3.up * groundRaycastHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                groundRaycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point + Vector3.up * spawnYOffset;

        Instantiate(_powerUpPrefab, spawnPos, Quaternion.identity);
    }
}
