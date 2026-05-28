using UnityEngine;

/// <summary>
/// Wijst elke wave één geest aan als Pack Leader.
/// Als de leider doodgaat: overige geesten -20% snelheid en healen 2× trager.
/// Als de leider NIET doodgaat voor wave-einde: overige geesten +10% snelheid.
/// </summary>
public class PackLeaderManager : MonoBehaviour
{
    public static PackLeaderManager Instance { get; private set; }

    [Header("On Leader Killed")]
    [Tooltip("Procentuele snelheidsafname voor overige geesten (-0.20 = −20%).")]
    [SerializeField, Range(0f, 0.5f)] private float speedDebuff    = 0.20f;
    [Tooltip("Factor waarmee het healtempo vertraagt (2 = 2× trager).")]
    [SerializeField, Range(1f, 5f)]   private float healSlowFactor = 2f;

    [Header("On Leader Survived")]
    [Tooltip("Procentuele snelheidstoename voor overige geesten (+0.10 = +10%).")]
    [SerializeField, Range(0f, 0.5f)] private float speedBuff      = 0.10f;

    private EnemyController _leader;
    private bool _leaderKilledThisWave;

    private void Awake() => Instance = this;

    /// <summary>Aanroepen vanuit EnemySpawner nadat alle wave-enemies gespawnd zijn.</summary>
    public void AssignLeader(EnemyController leader)
    {
        _leader = leader;
        _leaderKilledThisWave = false;
        leader.SetPackLeaderMode();
    }

    /// <summary>Aanroepen vanuit EnemyController.BeginDying() als de stervende geest de leider is.</summary>
    public void NotifyLeaderKilled()
    {
        if (_leaderKilledThisWave) return;
        _leaderKilledThisWave = true;
        _leader = null;
        EnemyController.BroadcastLeaderKilled(speedDebuff, healSlowFactor);
    }

    /// <summary>Aanroepen vanuit WaveManager.BeginBreak() om de wave-uitkomst af te handelen.</summary>
    public void NotifyWaveEnded()
    {
        if (!_leaderKilledThisWave && _leader != null)
            EnemyController.BroadcastLeaderSurvived(speedBuff);

        _leader = null;
        _leaderKilledThisWave = false;
    }
}
