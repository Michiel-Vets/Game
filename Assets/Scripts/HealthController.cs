using System;
using UnityEngine;

public class HealthController : MonoBehaviour
{
    [Header("Health Values")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Health Bar")]
    [SerializeField] private RectTransform healthBar;
    [SerializeField] private float fullWidth = 200f;
    [SerializeField] private float barHeight = 20f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (healthBar != null)
        {
            healthBar.pivot = new Vector2(0f, 0.5f);
            healthBar.anchorMin = new Vector2(0f, 0.5f);
            healthBar.anchorMax = new Vector2(0f, 0.5f);
        }

        UpdateHealthVisuals();
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
            return;

        // Ik verlies leven wanneer ik schade krijg.
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        UpdateHealthVisuals();

        if (currentHealth <= 0f)
        {
            // Ik ben dood, dus ik stuur een signaal naar andere scripts.
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f)
            return;

        // Ik krijg leven terug wanneer ik heal.
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateHealthVisuals();
    }

    public void ResetHealth()
    {
        // Ik reset mijn leven terug naar het maximum.
        currentHealth = maxHealth;
        UpdateHealthVisuals();
    }

    private void UpdateHealthVisuals()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (healthBar == null || maxHealth <= 0f)
            return;

        float normalizedHealth = currentHealth / maxHealth;
        float newWidth = normalizedHealth * fullWidth;
        healthBar.sizeDelta = new Vector2(newWidth, barHeight);
    }
}