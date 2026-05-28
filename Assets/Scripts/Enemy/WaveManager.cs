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
    [SerializeField] private float baseWaveDuration = 60f;
    [SerializeField] private float waveDurationPerEnemy = 4f;
    [SerializeField] private float maxWaveDuration = 240f;
    [SerializeField] private float initialBreakDuration = 20f;   // Rust voor wave 1
    [SerializeField] private float firstBreakDuration = 40f;     // Break na wave 1
    [SerializeField] private float breakDuration = 90f;          // Overige breaks

    [Header("Enemy Count Scaling (Normal)")]
    [SerializeField] private int baseMaxEnemies = 6;
    [SerializeField] private float enemyGrowthFactor = 1.18f;    // Exponentieel per wave
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

    [Header("Mist Pillar")]
    [Tooltip("Sleep hier het MistPillar-object uit de scène.")]
    [SerializeField] private MistPillar mistPillar;
    [Tooltip("Seconden vóór de wave dat de zuil verschijnt.")]
    [SerializeField] private float pillarShowBeforeWave = 10f;

    private bool _pillarShownThisBreak;

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
    public int TotalKillCount { get; private set; }

    public Vector3 NextWaveHintPosition
    {
        get
        {
            float mapRadius = MapController.Instance != null
                ? MapController.Instance.CurrentRadius - MapController.Instance.HardWallInset
                : 40f;
            Vector3 center = MapController.Instance != null
                ? MapController.Instance.PlatformCenter
                : Vector3.zero;
            Vector3 dir = _hasPrerolledDirection ? _prerolledSpawnDirection : Vector3.forward;
            return center + dir.normalized * mapRadius;
        }
    }

    // Wave-clear tracking
    private int _waveKillCount;
    private int _waveTotalSpawned;
    private int _totalWaveEnemies;       // totaal te spawnen deze wave (voor early-end check)
    private float _currentWaveDuration;  // opgeslagen bij wave start voor progress bar
    private float _pendingPenalty;       // multiplier > 1 als vorige wave niet gecleared was

    private EnemySpawner spawner;
    private WaveUIController waveUI;
    private bool difficultyApplied;
    private float scoutTimer;
    private int wavesSinceLastSpecial = 0;
    private Vector3 _prerolledSpawnDirection;
    private bool _hasPrerolledDirection;

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
        // Toon altijd het totale wave-aantal (ook niet-gespawnde enemies), behalve bij siege
        int displayTotal = (_totalWaveEnemies > 0 && _totalWaveEnemies < int.MaxValue)
            ? _totalWaveEnemies : _waveTotalSpawned;
        clearUI?.UpdateProgress(_waveKillCount, displayTotal);
    }

    /// <summary>Aanroepen zodra een enemy sterft (zaklamp of aanval).</summary>
    public void NotifyEnemyKilled()
    {
        TotalKillCount++;
        if (IsBreak) return;
        _waveKillCount++;
        int displayTotal = (_totalWaveEnemies > 0 && _totalWaveEnemies < int.MaxValue)
            ? _totalWaveEnemies : _waveTotalSpawned;
        clearUI?.UpdateProgress(_waveKillCount, displayTotal);
        ComboSystem.Instance?.NotifyKill();

        // Toon "WAVE CLEARED" pas als écht alle geplande wave-enemies dood zijn
        if (_totalWaveEnemies > 0 && _totalWaveEnemies < int.MaxValue
            && _waveKillCount >= _totalWaveEnemies)
            clearUI?.ShowWaveCleared();

        // Wave vroegtijdig beëindigen als alle enemies dood zijn
        if (_totalWaveEnemies > 0 && _waveKillCount >= _totalWaveEnemies)
            TimeRemaining = Mathf.Min(TimeRemaining, 2f);
    }

    private void Start()
    {
        spawner = FindObjectOfType<EnemySpawner>();
        waveUI = FindObjectOfType<WaveUIController>();
        IsBreak = true;
        TimeRemaining = initialBreakDuration;
        wavesSinceLastSpecial = 0;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        PrerollNextWaveDirection();
        // Geen OnBreakStarted() hier — power-ups spawnen pas na wave 1 (vanuit BeginBreak)
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
        if (!IsBreak)
        {
            if (progressUI != null)
            {
                float progress = _currentWaveDuration > 0f
                    ? 1f - (TimeRemaining / _currentWaveDuration)
                    : 1f;
                progressUI.SetProgress(progress);
            }
            progressUI?.HideBreakCountdown();
        }
        else if (CurrentWave > 0)
        {
            progressUI?.ShowBreakCountdown(TimeRemaining);
        }
        else
        {
            progressUI?.ShowBreakCountdown(TimeRemaining, isFirstWave: true);
        }

        if (TimeRemaining <= 0f)
        {
            if (IsBreak)
                BeginWave();
            else
                BeginBreak();
        }

        // Mist-zuil tonen als de volgende wave nadert
        if (IsBreak && CurrentWave > 0 && mistPillar != null && !_pillarShownThisBreak
            && TimeRemaining <= pillarShowBeforeWave)
        {
            _pillarShownThisBreak = true;
            mistPillar.TryShow(NextWaveHintPosition);
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

        // Reset kill-teller voor deze wave
        _waveKillCount    = 0;
        _waveTotalSpawned = 0;

        CurrentWaveType = DetermineWaveType();

        // Bepaal spawn richting (gebruik prerolled richting als beschikbaar)
        CurrentWaveSpawnDirection = _hasPrerolledDirection ? _prerolledSpawnDirection : GetWaveSpawnDirection();
        _hasPrerolledDirection = false;

        float diffScale = DifficultySettings.AggressionScaleMultiplier;
        AggressionLevel = Mathf.Clamp01((CurrentWave - 1) * aggressionPerWave * diffScale);

        int maxEnemies = GetWaveMaxEnemies();
        _totalWaveEnemies = CurrentWaveType == WaveType.Siege ? int.MaxValue : maxEnemies;
        _currentWaveDuration = GetWaveDuration(maxEnemies);
        TimeRemaining = _currentWaveDuration;

        float spawnInterval = GetWaveSpawnInterval();
        float enemyHealthMultiplier = GetEnemyHealthMultiplier();
        float enemySpeedMultiplier = GetEnemySpeedMultiplier();
        int spawnCap = GetWaveSpawnCap();

        // Speel audio voor speciale waves
        PlayWaveAudio();

        // Toon tooltip uitleg
        if (tooltipUI != null && CurrentWaveType != WaveType.Normal)
            tooltipUI.ShowTooltip(CurrentWaveType);

        mistPillar?.Hide();

        // Fog dichter tijdens wave
        VolumetricMistController.Instance?.SetBreakMode(false);

        // Start de clear bar
        clearUI?.OnWaveStarted(CurrentWaveType == WaveType.Siege);

        // Map laten groeien of krimpen afhankelijk van hoe de vorige wave verliep.
        // _pendingPenalty loopt van 0 (gecleared) tot maxClearPenalty (volledig gemist).
        float missedFraction = maxClearPenalty > 0f
            ? _pendingPenalty / maxClearPenalty
            : 0f;
        float powerUpModifier = PowerUpSpawner.Instance != null ? PowerUpSpawner.Instance.PickupFraction : 1f;
        PowerUpSpawner.Instance?.OnWaveStarted();
        MapController.Instance?.UpdateForWave(CurrentWave, missedFraction, powerUpModifier);
        if (powerUpModifier < 0.99f)
            progressUI?.ShowPowerUpFailText(powerUpModifier);

        spawner?.OnWaveStarted(CurrentWave, AggressionLevel, maxEnemies, spawnInterval,
                                CurrentWaveType, enemyHealthMultiplier, enemySpeedMultiplier, spawnCap,
                                CurrentWaveSpawnDirection);
        waveUI?.ShowWaveMessage(CurrentWave, CurrentWaveType);
    }

    private void PrerollNextWaveDirection()
    {
        int nextWave = CurrentWave + 1;
        if (useRandomDirection || waveSpawnPoints == null || waveSpawnPoints.Length == 0)
        {
            float angle = Random.Range(0f, 360f);
            _prerolledSpawnDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        }
        else
        {
            int index = (nextWave - 1) % waveSpawnPoints.Length;
            _prerolledSpawnDirection = waveSpawnPoints[index].position;
        }
        _hasPrerolledDirection = true;
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
        if (!IsBreak)
        {
            // Tijdens een actieve wave: stop de wave en start meteen de volgende
            BeginBreak();
        }
        // Skip = geen straf: map groeit altijd bij een skip
        _pendingPenalty = 0f;
        TimeRemaining = 0f;
    }

    public void AddBreakTime(float seconds)
    {
        if (!IsBreak) return;
        TimeRemaining += seconds;
    }

    private void BeginBreak()
    {
        IsBreak = true;
        _pillarShownThisBreak = false;
        float baseDur = CurrentWave == 1 ? firstBreakDuration : breakDuration;
        TimeRemaining = baseDur * DifficultySettings.BreakDurationMultiplier;
        scoutTimer = scoutSpawnInterval * 0.5f;
        spawner?.OnWaveBreak();
        PackLeaderManager.Instance?.NotifyWaveEnded();

        PrerollNextWaveDirection();
        PowerUpSpawner.Instance?.OnBreakStarted(NextWaveHintPosition);

        // Fog dunner tijdens break
        VolumetricMistController.Instance?.SetBreakMode(true);

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
        // Exponentiële groei: elke wave × enemyGrowthFactor
        int count = Mathf.RoundToInt(baseMaxEnemies * Mathf.Pow(enemyGrowthFactor, CurrentWave - 1));

        if (CurrentWaveType == WaveType.Horde)
            count = Mathf.RoundToInt(count * 1.8f);
        else if (CurrentWaveType == WaveType.Elite)
            count = Mathf.RoundToInt(count * 0.5f);

        // Straf voor niet-geclearde vorige wave: meer enemies
        count = Mathf.RoundToInt(count * mult * ClearPenaltyMult);
        return Mathf.Min(count, maxEnemyLimit);
    }

    private float GetWaveDuration(int enemyCount)
    {
        if (CurrentWaveType == WaveType.Siege)
            return baseWaveDuration;
        float duration = baseWaveDuration + waveDurationPerEnemy * enemyCount;
        return Mathf.Min(duration, maxWaveDuration);
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