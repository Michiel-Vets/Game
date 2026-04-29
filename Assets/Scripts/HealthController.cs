using System;
using UnityEngine;

public class HealthController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Health Bar")]
    [SerializeField] private RectTransform healthBar;
    [SerializeField] private float fullWidth = 200f;
    [SerializeField] private float barHeight = 20f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0f;

    public event Action OnDeath;
    public event Action<float, float> OnHealthChanged;

    private bool hasDied;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;

        SetupHealthBar();
        UpdateHealthVisuals();
    }

    public void TakeDamage(float amount)
    {
        if (hasDied || amount <= 0f)
            return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);

        UpdateHealthVisuals();

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (hasDied || amount <= 0f)
            return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateHealthVisuals();
    }

    public void ResetHealth()
    {
        hasDied = false;
        currentHealth = maxHealth;

        UpdateHealthVisuals();
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    private void Die()
    {
        if (hasDied)
            return;

        hasDied = true;
        currentHealth = 0f;

        UpdateHealthVisuals();

        Debug.Log("Player died. Game over event triggered.");

        OnDeath?.Invoke();
    }

    private void SetupHealthBar()
    {
        if (healthBar == null)
            return;

        healthBar.pivot = new Vector2(0f, 0.5f);
        healthBar.anchorMin = new Vector2(0f, 0.5f);
        healthBar.anchorMax = new Vector2(0f, 0.5f);
    }

    private void UpdateHealthVisuals()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (healthBar == null)
            return;

        float healthPercentage = GetHealthPercentage();
        float newWidth = fullWidth * healthPercentage;

        healthBar.sizeDelta = new Vector2(newWidth, barHeight);
    }
}