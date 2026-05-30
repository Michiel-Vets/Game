using UnityEngine;

public class PowerUpDropReward : MonoBehaviour
{
    private GameObject _powerUpPrefab;
    private bool _dropped;
    private static bool _appQuitting;

    private void OnApplicationQuit() => _appQuitting = true;

    public void Setup(GameObject powerUpPrefab)
    {
        _powerUpPrefab = powerUpPrefab;

        if (_powerUpPrefab == null)
            _powerUpPrefab = Resources.Load<GameObject>("Pickups/PowerUpPickup");

        if (_powerUpPrefab == null)
            Debug.LogError("PowerUpDropReward: geen power-up prefab gevonden. " +
                "Wijs 'Power Up Prefab' toe op PowerUpSpawner in de Inspector.");
    }

    /// <summary>Voorkomt dat de power-up dropt (aanroepen vóór Destroy bij despawn).</summary>
    public void CancelDrop() => _dropped = true;

    private void OnDestroy()
    {
        if (_dropped || _powerUpPrefab == null || _appQuitting || !Application.isPlaying) return;
        _dropped = true;

        // Spawn op de huidige positie van de geest — de pickup valt fysisch naar de grond
        GameObject pickup = Instantiate(_powerUpPrefab, transform.position, Quaternion.identity);

        Rigidbody rb = pickup.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = pickup.AddComponent<Rigidbody>();
            rb.angularDamping = 2f;
        }
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearDamping = 0f;
        // Geef een initiële neerwaartse snelheid zodat de pickup ~3× sneller valt
        rb.linearVelocity = Vector3.down * 12f;

        PowerUpSpawner.Instance?.RegisterPowerUp(pickup);
    }
}
