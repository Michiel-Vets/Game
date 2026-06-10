using UnityEngine;

/// <summary>
/// Gedeelde basis voor componenten die bij de dood van een enemy een pickup spawnen.
/// Subklassen implementeren <see cref="SpawnReward"/> voor het specifieke drop-gedrag.
/// </summary>
public abstract class BaseDropReward : MonoBehaviour
{
    private bool _dropped;
    private static bool _appQuitting;

    private void OnApplicationQuit() => _appQuitting = true;

    /// <summary>Voorkomt dat er een drop plaatsvindt (aanroepen vóór Destroy bij despawn).</summary>
    public void CancelDrop() => _dropped = true;

    protected virtual void OnDestroy()
    {
        if (_dropped || _appQuitting || !Application.isPlaying) return;
        _dropped = true;
        SpawnReward();
    }

    /// <summary>Spawn de pickup. Wordt aangeroepen vanuit OnDestroy als de drop niet geannuleerd is.</summary>
    protected abstract void SpawnReward();
}
