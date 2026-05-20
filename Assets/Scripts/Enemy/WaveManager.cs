using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Timing")]
    [SerializeField] private float waveDuration = 120f;
    [SerializeField] private float breakDuration = 120f;

    [Header("Enemy Count Scaling")]
    [SerializeField] private int baseMaxEnemies = 3;
    [SerializeField] private int enemyCountIncreasePerWave = 2;
    [SerializeField] private int maxEnemyLimit = 50;

    [Header("Spawn Interval Scaling")]
    [SerializeField] private float baseSpawnInterval = 8f;
    [SerializeField] private float minSpawnInterval = 0.8f;
    [SerializeField] private float spawnIntervalDecreasePerWave = 0.4f;

    [Header("Aggression")]
    [SerializeField] private float aggressionPerWave = 0.09f;

    public int CurrentWave { get; private set; }
    public bool IsBreak { get; private set; }
    public float TimeRemaining { get; private set; }
    public float AggressionLevel { get; private set; }

    private EnemySpawner spawner;
    private WaveUIController waveUI;
    private bool difficultyApplied;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        spawner = FindObjectOfType<EnemySpawner>();
        waveUI = FindObjectOfType<WaveUIController>();
        IsBreak = true;
        TimeRemaining = 0.1f;
    }

    private void Update()
    {
        if (!difficultyApplied)
        {
            if (Time.timeScale <= 0f) return;
            DifficultySettings.Load();
            difficultyApplied = true;
        }

        TimeRemaining -= Time.deltaTime;

        if (TimeRemaining <= 0f)
        {
            if (IsBreak)
                BeginWave();
            else
                BeginBreak();
        }
    }

    private void BeginWave()
    {
        CurrentWave++;
        IsBreak = false;
        TimeRemaining = waveDuration;

        float diffScale = DifficultySettings.AggressionScaleMultiplier;
        AggressionLevel = Mathf.Clamp01((CurrentWave - 1) * aggressionPerWave * diffScale);

        int maxEnemies = GetWaveMaxEnemies();
        float spawnInterval = GetWaveSpawnInterval();

        spawner?.OnWaveStarted(CurrentWave, AggressionLevel, maxEnemies, spawnInterval);
        waveUI?.ShowWaveMessage(CurrentWave);
    }

    private void BeginBreak()
    {
        IsBreak = true;
        TimeRemaining = breakDuration * DifficultySettings.BreakDurationMultiplier;
        spawner?.OnWaveBreak();
    }

    public int GetWaveMaxEnemies()
    {
        float mult = DifficultySettings.WaveEnemyCountMultiplier;
        int count = baseMaxEnemies + (CurrentWave - 1) * enemyCountIncreasePerWave;
        return Mathf.Min(Mathf.RoundToInt(count * mult), maxEnemyLimit);
    }

    public float GetWaveSpawnInterval()
    {
        float interval = baseSpawnInterval - (CurrentWave - 1) * spawnIntervalDecreasePerWave;
        return Mathf.Max(interval * DifficultySettings.SpawnIntervalMultiplier, minSpawnInterval);
    }
}
