using UnityEngine;

public class HealthPickup : BasePickup
{
    [SerializeField] private float healAmount = 25f;

    private void OnTriggerEnter(Collider other)
    {
        HealthController health = other.GetComponentInParent<HealthController>();
        if (health == null || !health.CompareTag("Player")) return;
        if (health.HealthPercentage >= 1f) return;
        if (ComboSystem.Instance != null && ComboSystem.Instance.IsOverused) return;

        health.Heal(healAmount);
        Destroy(gameObject);
    }
}
