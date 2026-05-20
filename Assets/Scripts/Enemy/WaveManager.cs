using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Break Timing")]
    [SerializeField] private float initialBreakDuration = 60f;
    [SerializeField] private float minBreakDuration = 8f;
    [SerializeField] private float breakReductionPerWave = 3f;

    [Header("Wave Timeout")]
    [SerializeField] private float waveTimeoutDuration = 240f;

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

    [Header("Special Waves")]
    [SerializeField] private int specialWaveStartInterval = 5;
    [SerializeField] private int wavesBeforeIntervalDecrease = 5;

    [Header("Wave Completion")]
    [SerializeField] private float completionBatteryRestoreFraction = 0.2f;
    [SerializeField] private float penaltyEnemyMultiplier = 1.5f;
    [SerializeField] private float penaltySpawnIntervalMultiplier = 0.7f;

    [Header("Scouts")]
    [SerializeField] private float scoutSpawnBreakFraction = 0.35f;
    [SerializeField] private int scoutSpawnCount = 2;

    public int CurrentWave { get; private set; }
    public bool IsBreak { get; private set; }
    public float TimeRemaining { get; private set; }
    public float AggressionLevel { get; private set; }
    public WaveType CurrentWaveType { get; private set; }
    public int Score { get; private set; }

    private EnemySpawner spawner;
    private WaveUIController waveUI;
    private BatteryController battery;
    private bool difficultyApplied;

    private float currentBreakDuration;
    private float waveTimer;
    private bool scoutsSpawned;
    private bool penalizeNextWave;
    private float nextWavePenaltyMult = 1f;

    private void Awake() => Instance = this;

    private void Start()
    {
        spawner = FindObjectOfType<EnemySpawner>();
        waveUI = FindObjectOfType<WaveUIController>();
        battery = FindObjectOfType<BatteryController>();
        IsBreak = true;
        currentBreakDuration = initialBreakDuration;
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

        if (IsBreak)
            UpdateBreak();
        else
            UpdateWave();
    }

    private void UpdateBreak()
    {
        TimeRemaining -= Time.deltaTime;

        float elapsed = currentBreakDuration - TimeRemaining;
        if (!scoutsSpawned && elapsed >= currentBreakDuration * scoutSpawnBreakFraction)
        {
            scoutsSpawned = true;
            spawner?.SpawnScouts(scoutSpawnCount, AggressionLevel);
        }

        if (TimeRemaining <= 0f)
            BeginWave();
    }

    private void UpdateWave()
    {
        waveTimer += Time.deltaTime;

        if (spawner != null && spawner.AllEnemiesDead)
        {
            EndWave(true);
            return;
        }

        float timeout = waveTimeoutDuration * DifficultySettings.WaveTimeoutMultiplier;
        if (waveTimer >= timeout)
            EndWave(false);
    }

    private void BeginWave()
    {
        CurrentWave++;
        IsBreak = false;
        waveTimer = 0f;
        scoutsSpawned = false;

        float diffScale = DifficultySettings.AggressionScaleMultiplier;
        AggressionLevel = Mathf.Clamp01((CurrentWave - 1) * aggressionPerWave * diffScale);

        CurrentWaveType = DetermineWaveType();

        int maxEnemies = ComputeBaseMaxEnemies();
        float spawnInterval = ComputeBaseSpawnInterval();

        ApplyWaveTypeModifiers(CurrentWaveType, ref maxEnemies, ref spawnInterval);

        if (penalizeNextWave)
        {
            maxEnemies = Mathf.Min(Mathf.RoundToInt(maxEnemies * nextWavePenaltyMult), maxEnemyLimit);
            spawnInterval = Mathf.Max(spawnInterval * penaltySpawnIntervalMultiplier, minSpawnInterval);
            penalizeNextWave = false;
            nextWavePenaltyMult = 1f;
        }

        bool isSiege = (CurrentWaveType & WaveType.Siege) != 0;
        bool isElite = (CurrentWaveType & WaveType.Elite) != 0;
        float attackDirection = Random.Range(0f, 360f);

        spawner?.OnWaveStarted(CurrentWave, AggressionLevel, maxEnemies, spawnInterval, isSiege, isElite, attackDirection);
        waveUI?.ShowWaveMessage(CurrentWave, CurrentWaveType);
    }

    private void EndWave(bool completed)
    {
        IsBreak = true;
        spawner?.OnWaveBreak();

        if (completed)
        {
            int points = ComputeBaseMaxEnemies() * 100 * CurrentWave;
            Score += points;
            if (battery != null)
                battery.RechargeBattery(completionBatteryRestoreFraction * battery.MaxBattery);
            waveUI?.ShowCompletionMessage(points);
        }
        else
        {
            penalizeNextWave = true;
            nextWavePenaltyMult = penaltyEnemyMultiplier + CurrentWave * 0.03f;
            waveUI?.ShowPenaltyMessage();
        }

        currentBreakDuration = ComputeBreakDuration();
        TimeRemaining = currentBreakDuration;
    }

    private WaveType DetermineWaveType()
    {
        int interval = GetSpecialWaveInterval();
        if (CurrentWave % interval != 0)
            return WaveType.Standard;

        var possibleTypes = new[] { WaveType.Horde, WaveType.Elite, WaveType.Siege };
        int maxCombos = Mathf.Clamp(1 + CurrentWave / 15, 1, possibleTypes.Length);

        var pool = new List<WaveType>(possibleTypes);
        WaveType result = WaveType.Standard;
        int picks = Random.Range(1, maxCombos + 1);

        for (int i = 0; i < picks && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result |= pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }

    private int GetSpecialWaveInterval()
    {
        int reductions = CurrentWave / wavesBeforeIntervalDecrease;
        return Mathf.Max(1, specialWaveStartInterval - reductions);
    }

    private float ComputeBreakDuration()
    {
        float reduced = initialBreakDuration - (CurrentWave - 1) * breakReductionPerWave;
        return Mathf.Max(reduced, minBreakDuration) * DifficultySettings.BreakDurationMultiplier;
    }

    private int ComputeBaseMaxEnemies()
    {
        int count = baseMaxEnemies + (CurrentWave - 1) * enemyCountIncreasePerWave;
        return Mathf.Min(Mathf.RoundToInt(count * DifficultySettings.WaveEnemyCountMultiplier), maxEnemyLimit);
    }

    private float ComputeBaseSpawnInterval()
    {
        float interval = baseSpawnInterval - (CurrentWave - 1) * spawnIntervalDecreasePerWave;
        return Mathf.Max(interval * DifficultySettings.SpawnIntervalMultiplier, minSpawnInterval);
    }

    private void ApplyWaveTypeModifiers(WaveType type, ref int maxEnemies, ref float spawnInterval)
    {
        if ((type & WaveType.Horde) != 0)
        {
            maxEnemies = Mathf.Min(Mathf.RoundToInt(maxEnemies * 2f), maxEnemyLimit);
            spawnInterval = Mathf.Max(spawnInterval * 0.45f, minSpawnInterval);
        }
        if ((type & WaveType.Elite) != 0)
        {
            maxEnemies = Mathf.Max(2, Mathf.RoundToInt(maxEnemies * 0.4f));
        }
    }

    public int GetWaveMaxEnemies() => ComputeBaseMaxEnemies();
    public float GetWaveSpawnInterval() => ComputeBaseSpawnInterval();
}