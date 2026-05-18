using UnityEngine;

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

    public void Tick(float deltaTime)
    {
        if (remaining > 0f)
            remaining -= deltaTime;
    }

    public void Reset()
    {
        remaining = duration;
    }

    public void Reset(float customDuration)
    {
        remaining = customDuration;
    }

    public void ResetRandom(float min, float max)
    {
        remaining = Random.Range(min, max);
    }

    public void ForceReady()
    {
        remaining = 0f;
    }
}