using UnityEngine;
using System.Collections.Generic;

public enum WaveType
{
    Normal,
    Siege,    // Non-stop spawnen, geen max limit
    Horde,    // Meer enemies, minder HP
    Elite     // Minder enemies, sterkere stats
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Timing")]
    [SerializeField] private float waveDuration = 120f;
    [SerializeField] private float breakDuration = 120f;

    [Header("Enemy Count Scaling (Normal)")]
    [SerializeField] private int baseMaxEnemies = 5;          // Hoger: agressiever begin
    [SerializeField] private int enemyCountIncreasePerWave = 2;
    [SerializeField] private int maxEnemyLimit = 50;

    [Header("Spawn Interval Scaling")]
    [SerializeField] private float baseSpawnInterval = 5f;    // Lager: sneller spawnen
    [SerializeField] private float minSpawnInterval = 0.8f;
    [SerializeField] private float spawnIntervalDecreasePerWave = 0.3f;

    [Header("Aggression")]
    [SerializeField] private float aggressionPerWave = 0.12f; // Sneller agressief

    [Header("Special Waves")]
    [SerializeField] private int initialSpecialWaveInterval = 3; // Elke 3e wave speciaal
    [SerializeField] private int minSpecialWaveInterval = 1;     // Uiteindelijk elke wave
    [SerializeField] private float specialWaveIntervalDecreasePerWave = 0.05f;

    [Header("Scouts During Break")]
    [SerializeField] private int maxScoutsDuringBreak = 3;
    [SerializeField] private float scoutSpawnInterval = 12f;

    // Runtime
    public int CurrentWave { get; private set; }
    public bool IsBreak { get; private set; }
    public float TimeRemaining { get; private set; }
    public float AggressionLevel { get; private set; }
    public WaveType CurrentWaveType { get; private set; }
    public bool IsSpecialWave => CurrentWaveType != WaveType.Normal;

    private EnemySpawner spawner;
    private WaveUIController waveUI;
    private bool difficultyApplied;
    private float scoutTimer;
    private int wavesSinceLastSpecial = 0;

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
        wavesSinceLastSpecial = 0;
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

        // Scouts tijdens pauze
        if (IsBreak && spawner != null)
        {
            scoutTimer -= Time.deltaTime;
            if (scoutTimer <= 0f)
            {
                scoutTimer = scoutSpawnInterval * DifficultySettings.ScoutSpawnIntervalMultiplier;
                spawner.SpawnScout();
            }
        }
    }

    private void BeginWave()
    {
        CurrentWave++;
        IsBreak = false;
        TimeRemaining = waveDuration;

        // Bepaal wave type
        CurrentWaveType = DetermineWaveType();

        float diffScale = DifficultySettings.AggressionScaleMultiplier;
        AggressionLevel = Mathf.Clamp01((CurrentWave - 1) * aggressionPerWave * diffScale);

        // Configuratie afhankelijk van wave type
        int maxEnemies = GetWaveMaxEnemies();
        float spawnInterval = GetWaveSpawnInterval();
        float enemyHealthMultiplier = GetEnemyHealthMultiplier();
        float enemySpeedMultiplier = GetEnemySpeedMultiplier();
        int spawnCap = GetWaveSpawnCap();

        spawner?.OnWaveStarted(CurrentWave, AggressionLevel, maxEnemies, spawnInterval,
                                CurrentWaveType, enemyHealthMultiplier, enemySpeedMultiplier, spawnCap);
        waveUI?.ShowWaveMessage(CurrentWave, CurrentWaveType);
    }

    private WaveType DetermineWaveType()
    {
        // Bereken huidige interval tussen speciale waves
        int intervalBonus = DifficultySettings.SpecialWaveIntervalBonus;
        int interval = Mathf.Max(minSpecialWaveInterval,
            Mathf.RoundToInt(initialSpecialWaveInterval - (CurrentWave - 1) * specialWaveIntervalDecreasePerWave) + intervalBonus);

        wavesSinceLastSpecial++;

        // Nog geen special wave deze ronde?
        if (wavesSinceLastSpecial < interval && CurrentWave > 1)
            return WaveType.Normal;

        // Reset teller
        wavesSinceLastSpecial = 0;

        // Kies een special wave type (later combinaties)
        return ChooseSpecialWaveType();
    }

    private WaveType ChooseSpecialWaveType()
    {
        // Naarmate waves vorderen, kans op mix van types
        float mixChance = Mathf.Clamp01((CurrentWave - 5) / 20f); // Vanaf wave 5, oplopend tot ~wave 25

        if (mixChance > Random.value && CurrentWave > 3)
        {
            // Mix van twee types: kies er twee, geef priority aan de eerste
            WaveType primary = (WaveType)Random.Range(1, 4);
            WaveType secondary = (WaveType)Random.Range(1, 4);
            // Combinatie wordt afgehandeld in EnemySpawner via wave modifiers
            return primary; // We geven primary door, spawner leest mix uit CurrentWaveSecondaryType
        }

        return (WaveType)Random.Range(1, 4);
    }

    public WaveType GetSecondaryWaveType()
    {
        if (CurrentWave <= 3) return WaveType.Normal;
        float mixChance = Mathf.Clamp01((CurrentWave - 5) / 20f);
        if (mixChance > Random.value)
            return (WaveType)Random.Range(1, 4);
        return WaveType.Normal;
    }

    private void BeginBreak()
    {
        IsBreak = true;
        TimeRemaining = breakDuration * DifficultySettings.BreakDurationMultiplier;
        scoutTimer = scoutSpawnInterval * 0.5f; // Eerste scout snel
        spawner?.OnWaveBreak();
    }

    public int GetWaveMaxEnemies()
    {
        if (CurrentWaveType == WaveType.Siege)
            return 999; // Geen limiet tijdens siege

        float mult = DifficultySettings.WaveEnemyCountMultiplier;
        int count = baseMaxEnemies + (CurrentWave - 1) * enemyCountIncreasePerWave;

        if (CurrentWaveType == WaveType.Horde)
            count = Mathf.RoundToInt(count * 1.8f);
        else if (CurrentWaveType == WaveType.Elite)
            count = Mathf.RoundToInt(count * 0.5f);

        return Mathf.Min(Mathf.RoundToInt(count * mult), maxEnemyLimit);
    }

    public float GetWaveSpawnInterval()
    {
        float interval = baseSpawnInterval - (CurrentWave - 1) * spawnIntervalDecreasePerWave;

        if (CurrentWaveType == WaveType.Siege)
            interval *= 0.6f; // Sneller spawnen tijdens siege
        else if (CurrentWaveType == WaveType.Horde)
            interval *= 0.7f;

        return Mathf.Max(interval * DifficultySettings.SpawnIntervalMultiplier, minSpawnInterval);
    }

    public float GetEnemyHealthMultiplier()
    {
        float baseMult = 1f;
        if (CurrentWaveType == WaveType.Horde)
            baseMult = 0.6f;
        else if (CurrentWaveType == WaveType.Elite)
            baseMult = 2.2f;

        // Difficulty factor
        baseMult *= DifficultySettings.EnemyHealthMultiplier;
        return baseMult;
    }

    public float GetEnemySpeedMultiplier()
    {
        float baseMult = 1f;
        if (CurrentWaveType == WaveType.Elite)
            baseMult = 0.8f; // Elite is trager maar sterker
        else if (CurrentWaveType == WaveType.Horde)
            baseMult = 1.15f;

        baseMult *= DifficultySettings.EnemySpeedMultiplier;
        return baseMult;
    }

    public int GetWaveSpawnCap()
    {
        if (CurrentWaveType == WaveType.Siege)
            return 999;
        return GetWaveMaxEnemies();
    }
}