using UnityEngine;

/// <summary>
/// Lichtgewicht cooldown-timer zonder MonoBehaviour.
/// Gebruik Tick() in Update/FixedUpdate en check IsReady.
/// </summary>
[System.Serializable]
public class Cooldown
{
    [SerializeField] private float duration;

    private float remaining;

    public bool IsReady => remaining <= 0f;
    public float Remaining => remaining;
    public float Duration => duration;

    public Cooldown() { }

    public Cooldown(float duration)
    {
        this.duration = duration;
    }

    /// <summary>Geeft de timer door met deltaTime. Gebruik in Update of FixedUpdate.</summary>
    public void Tick(float deltaTime)
    {
        if (remaining > 0f)
            remaining -= deltaTime;
    }

    /// <summary>Herstart de cooldown met de standaard duur.</summary>
    public void Reset()
    {
        remaining = duration;
    }

    /// <summary>Herstart de cooldown met een aangepaste duur.</summary>
    public void Reset(float customDuration)
    {
        remaining = customDuration;
    }

    /// <summary>Herstart met een willekeurige duur tussen min en max.</summary>
    public void ResetRandom(float min, float max)
    {
        remaining = Random.Range(min, max);
    }

    /// <summary>Zet de timer op klaar (remaining = 0).</summary>
    public void ForceReady()
    {
        remaining = 0f;
    }
}