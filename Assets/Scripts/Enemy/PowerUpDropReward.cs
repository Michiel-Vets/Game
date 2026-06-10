using UnityEngine;

public class PowerUpDropReward : BaseDropReward
{
    private GameObject _powerUpPrefab;

    public void Setup(GameObject powerUpPrefab)
    {
        _powerUpPrefab = powerUpPrefab;

        if (_powerUpPrefab == null)
            _powerUpPrefab = Resources.Load<GameObject>("Pickups/PowerUpPickup");

        if (_powerUpPrefab == null)
            Debug.LogError("PowerUpDropReward: geen power-up prefab gevonden. " +
                "Wijs 'Power Up Prefab' toe op PowerUpSpawner in de Inspector.");
    }

    protected override void SpawnReward()
    {
        if (_powerUpPrefab == null) return;

        GameObject pickup = Instantiate(_powerUpPrefab, transform.position, Quaternion.identity);

        Rigidbody rb = pickup.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = pickup.AddComponent<Rigidbody>();
            rb.angularDamping = 2f;
        }
        rb.useGravity     = true;
        rb.isKinematic    = false;
        rb.linearDamping  = 0f;
        rb.linearVelocity = Vector3.down * 12f;

        PowerUpSpawner.Instance?.RegisterPowerUp(pickup);
    }
}
