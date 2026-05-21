using UnityEngine;

public enum WaveType
{
    Normal,
    Siege,
    Horde,
    Elite
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Timing")]
    [SerializeField] private float waveDuration = 120f;
    [SerializeField] private float breakDuration = 120f;

    [Header("Enemy Count Scaling (Normal)")]
    [SerializeField] private int baseMaxEnemies = 5;
    [SerializeField] private int enemyCountIncreasePerWave = 2;
    [SerializeField] private int maxEnemyLimit = 50;

    [Header("Spawn Interval Scaling")]
    [SerializeField] private float baseSpawnInterval = 5f;
    [SerializeField] private float minSpawnInterval = 0.8f;
    [SerializeField] private float spawnIntervalDecreasePerWave = 0.3f;

    [Header("Aggression")]
    [SerializeField] private float aggressionPerWave = 0.12f;

    [Header("Special Waves")]
    [SerializeField] private int initialSpecialWaveInterval = 3;
    [SerializeField] private int minSpecialWaveInterval = 1;
    [SerializeField] private float specialWaveIntervalDecreasePerWave = 0.05f;

    [Header("Scouts During Break")]
    [SerializeField] private float scoutSpawnInterval = 12f;

    [Header("Wave Direction")]
    [SerializeField] private Transform[] waveSpawnPoints; // Pre-determined spawn points per wave
    [SerializeField] private bool useRandomDirection = false;

    [Header("Audio")]
    [SerializeField] private AudioClip siegeWaveSound;
    [SerializeField] private AudioClip hordeWaveSound;
    [SerializeField] private AudioClip eliteWaveSound;
    [SerializeField] private AudioSource audioSource;

    [Header("UI")]
    [SerializeField] private WaveProgressUI progressUI;
    [SerializeField] private WaveTooltipUI tooltipUI;
    [SerializeField] private WaveClearUI clearUI;

    [Header("Wave Clear Penalty")]
    [Tooltip("Maximale extra moeilijkheidsboost als een wave helemaal niet gecleared wordt (0–1).")]
    [SerializeField, Range(0f, 1f)] private float maxClearPenalty = 0.4f;
    [Tooltip("Siege waves tellen niet mee voor de clear-penalty.")]
    [SerializeField] private bool siegeWavesExemptFromPenalty = true;

    // Runtime
    public int CurrentWave { get; private set; }
    public bool IsBreak { get; private set; }
    public float TimeRemaining { get; private set; }
    public float AggressionLevel { get; private set; }
    public WaveType CurrentWaveType { get; private set; }
    public bool IsSpecialWave => CurrentWaveType != WaveType.Normal;
    public Vector3 CurrentWaveSpawnDirection { get; private set; }

    // Wave-clear tracking
    private int _waveKillCount;
    private int _waveTotalSpawned;
    private float _pendingPenalty;       // multiplier > 1 als vorige wave niet gecleared was

    private EnemySpawner spawner;
    private WaveUIController waveUI;
    private bool difficultyApplied;
    private float scoutTimer;
    private int wavesSinceLastSpecial = 0;

    // Gemak-property zodat multiplier-methodes schoon blijven
    private float ClearPenaltyMult => 1f + _pendingPenalty;

    private void Awake()
    {
        Instance = this;
    }

    // ── Kill / spawn tracking (aangeroepen vanuit EnemyController & EnemySpawner) ──

    /// <summary>Aanroepen zodra een wave-enemy gespawnd wordt (niet scouts).</summary>
    public void NotifyEnemySpawned()
    {
        if (IsBreak) return;
        _waveTotalSpawned++;
        clearUI?.UpdateProgress(_waveKillCount, _waveTotalSpawned);
    }

    /// <summary>Aanroepen zodra een enemy sterft (zaklamp of aanval).</summary>
    public void NotifyEnemyKilled()
    {
        if (IsBreak) return;
        _waveKillCount++;
        clearUI?.UpdateProgress(_waveKillCount, _waveTotalSpawned);

        if (_waveTotalSpawned > 0 && _waveKillCount >= _waveTotalSpawned)
            clearUI?.ShowWaveCleared();
    }

    private void Start()
    {
        spawner = FindObjectOfType<EnemySpawner>();
        waveUI = FindObjectOfType<WaveUIController>();
        IsBreak = true;
        TimeRemaining = 0.1f;
        wavesSinceLastSpecial = 0;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
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

        // Update progress UI
        if (progressUI != null && !IsBreak)
        {
            float progress = 1f - (TimeRemaining / waveDuration);
            progressUI.SetProgress(progress);
        }

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

        // Reset kill-teller voor deze wave
        _waveKillCount    = 0;
        _waveTotalSpawned = 0;

        CurrentWaveType = DetermineWaveType();

        // Bepaal spawn richting
        CurrentWaveSpawnDirection = GetWaveSpawnDirection();

        float diffScale = DifficultySettings.AggressionScaleMultiplier;
        AggressionLevel = Mathf.Clamp01((CurrentWave - 1) * aggressionPerWave * diffScale);

        int maxEnemies = GetWaveMaxEnemies();
        float spawnInterval = GetWaveSpawnInterval();
        float enemyHealthMultiplier = GetEnemyHealthMultiplier();
        float enemySpeedMultiplier = GetEnemySpeedMultiplier();
        int spawnCap = GetWaveSpawnCap();

        // Speel audio voor speciale waves
        PlayWaveAudio();

        // Toon tooltip uitleg
        if (tooltipUI != null && CurrentWaveType != WaveType.Normal)
            tooltipUI.ShowTooltip(CurrentWaveType);

        // Start de clear bar
        clearUI?.OnWaveStarted(CurrentWaveType == WaveType.Siege);

        spawner?.OnWaveStarted(CurrentWave, AggressionLevel, maxEnemies, spawnInterval,
                                CurrentWaveType, enemyHealthMultiplier, enemySpeedMultiplier, spawnCap,
                                CurrentWaveSpawnDirection);
        waveUI?.ShowWaveMessage(CurrentWave, CurrentWaveType);
    }

    private Vector3 GetWaveSpawnDirection()
    {
        if (useRandomDirection || waveSpawnPoints == null || waveSpawnPoints.Length == 0)
        {
            float angle = Random.Range(0f, 360f);
            return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        }

        int index = (CurrentWave - 1) % waveSpawnPoints.Length;
        return waveSpawnPoints[index].position;
    }

    private void PlayWaveAudio()
    {
        if (audioSource == null) return;

        switch (CurrentWaveType)
        {
            case WaveType.Siege:
                if (siegeWaveSound != null) audioSource.PlayOneShot(siegeWaveSound);
                break;
            case WaveType.Horde:
                if (hordeWaveSound != null) audioSource.PlayOneShot(hordeWaveSound);
                break;
            case WaveType.Elite:
                if (eliteWaveSound != null) audioSource.PlayOneShot(eliteWaveSound);
                break;
        }
    }

    private WaveType DetermineWaveType()
    {
        // Wave 1 is altijd normaal
        if (CurrentWave == 1)
            return WaveType.Normal;

        int intervalBonus = DifficultySettings.SpecialWaveIntervalBonus;
        int interval = Mathf.Max(minSpecialWaveInterval,
            Mathf.RoundToInt(initialSpecialWaveInterval - (CurrentWave - 1) * specialWaveIntervalDecreasePerWave) + intervalBonus);

        wavesSinceLastSpecial++;

        if (wavesSinceLastSpecial < interval)
            return WaveType.Normal;

        wavesSinceLastSpecial = 0;
        return ChooseSpecialWaveType();
    }

    private WaveType ChooseSpecialWaveType()
    {
        float mixChance = Mathf.Clamp01((CurrentWave - 5) / 20f);
        mixChance += DifficultySettings.SpecialWaveMixChanceBonus;
        mixChance = Mathf.Clamp01(mixChance);

        if (mixChance > Random.value && CurrentWave > 3)
        {
            // Mix wave - primary type
            return (WaveType)Random.Range(1, 4);
        }

        return (WaveType)Random.Range(1, 4);
    }

    public void SkipToNextWave()
    {
        if (!IsBreak) return;
        TimeRemaining = 0f;
    }

    private void BeginBreak()
    {
        IsBreak = true;
        TimeRemaining = breakDuration * DifficultySettings.BreakDurationMultiplier;
        scoutTimer = scoutSpawnInterval * 0.5f;
        spawner?.OnWaveBreak();

        if (progressUI != null)
            progressUI.Hide();

        // ── Wave-clear penalty berekening ────────────────────────────────────
        bool exemptSiege = siegeWavesExemptFromPenalty && CurrentWaveType == WaveType.Siege;
        if (!exemptSiege && _waveTotalSpawned > 0)
        {
            float clearFraction = Mathf.Clamp01((float)_waveKillCount / _waveTotalSpawned);
            float missedFraction = 1f - clearFraction;
            _pendingPenalty = missedFraction * maxClearPenalty;

            if (_pendingPenalty > 0.01f)
                clearUI?.ShowPenalty(ClearPenaltyMult);
            else
                clearUI?.OnWaveEnded();
        }
        else
        {
            _pendingPenalty = 0f;
            clearUI?.OnWaveEnded();
        }
    }

    public int GetWaveMaxEnemies()
    {
        if (CurrentWaveType == WaveType.Siege)
            return 999;

        float mult = DifficultySettings.WaveEnemyCountMultiplier;
        int count = baseMaxEnemies + (CurrentWave - 1) * enemyCountIncreasePerWave;

        if (CurrentWaveType == WaveType.Horde)
            count = Mathf.RoundToInt(count * 1.8f);
        else if (CurrentWaveType == WaveType.Elite)
            count = Mathf.RoundToInt(count * 0.5f);

        // Straf voor niet-geclearde vorige wave: meer enemies
        count = Mathf.RoundToInt(count * mult * ClearPenaltyMult);
        return Mathf.Min(count, maxEnemyLimit);
    }

    public float GetWaveSpawnInterval()
    {
        float interval = baseSpawnInterval - (CurrentWave - 1) * spawnIntervalDecreasePerWave;

        if (CurrentWaveType == WaveType.Siege)
            interval *= 0.6f;
        else if (CurrentWaveType == WaveType.Horde)
            interval *= 0.7f;

        // Straf: sneller spawnen (interval delen door penalty multiplier)
        return Mathf.Max(interval * DifficultySettings.SpawnIntervalMultiplier / ClearPenaltyMult,
                         minSpawnInterval);
    }

    public float GetEnemyHealthMultiplier()
    {
        float baseMult = 1f;
        if (CurrentWaveType == WaveType.Horde)
            baseMult = 0.6f;
        else if (CurrentWaveType == WaveType.Elite)
            baseMult = 2.2f;

        baseMult *= DifficultySettings.EnemyHealthMultiplier;
        return baseMult;
    }

    public float GetEnemySpeedMultiplier()
    {
        float baseMult = 1f;
        if (CurrentWaveType == WaveType.Elite)
            baseMult = 0.8f;
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