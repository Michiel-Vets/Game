using System.Collections.Generic;
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
    [SerializeField] private float firstBreakDuration = 60f;     // Break na wave 1
    [SerializeField] private float breakDuration = 150f;         // Overige breaks

    [Header("Enemy Count Scaling (Normal)")]
    [SerializeField] private int baseMaxEnemies = 6;
    [SerializeField] private float enemyGrowthFactor = 1.40f;
    [SerializeField] private int maxEnemyLimit = 60;

    [Header("Spawn Interval Scaling")]
    [SerializeField] private float baseSpawnInterval = 4f;
    [SerializeField] private float minSpawnInterval = 0.6f;
    [SerializeField] private float spawnIntervalDecreasePerWave = 0.35f;

    [Header("Aggression")]
    [SerializeField] private float aggressionPerWave = 0.15f;

    [Header("Special Waves")]
    [SerializeField] private int initialSpecialWaveInterval = 2;
    [SerializeField] private int minSpecialWaveInterval = 1;
    [SerializeField] private float specialWaveIntervalDecreasePerWave = 0.05f;

    [Header("Scouts During Break")]
    [SerializeField] private float scoutSpawnInterval = 12f;

    [Header("Wave Direction")]
    [SerializeField] private Transform[] waveSpawnPoints;
    [SerializeField] private bool useRandomDirection = false;

    [Header("Multi-Direction Wave Spawning")]
    [Tooltip("Minimum aantal richtingen waaruit vijanden per wave aanvallen.")]
    [SerializeField] private int minGroupCount = 2;
    [Tooltip("Maximum aantal richtingen.")]
    [SerializeField] private int maxGroupCount = 3;
    [Tooltip("Seconden vertraging tussen opeenvolgende groepen.")]
    [SerializeField] private float interGroupDelay = 20f;
    [Tooltip("Willekeurige variatie op de groepsvertraging (± seconden).")]
    [SerializeField] private float interGroupDelayVariance = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip siegeWaveSound;
    [SerializeField] private AudioClip hordeWaveSound;
    [SerializeField] private AudioClip eliteWaveSound;
    [SerializeField] private AudioSource audioSource;

    [Header("UI")]
    [SerializeField] private WaveProgressUI progressUI;
    [SerializeField] private WaveTooltipUI tooltipUI;
    [SerializeField] private WaveClearUI clearUI;

    [Header("Dynamic Aggression (Kill & Wave-Progress Scaling)")]
    [Tooltip("Aantal kills waarna de max kill-aggressiebonus bereikt wordt.")]
    [SerializeField] private float killsForMaxAggressionBonus = 80f;
    [Tooltip("Max extra aggressie door kills (0–1).")]
    [SerializeField] private float maxKillAggressionBonus = 0.55f;
    [Tooltip("Max extra aggressie door voortgang binnen een wave (0–1).")]
    [SerializeField] private float maxInWaveAggressionBonus = 0.45f;
    [Tooltip("Hoe snel de in-wave bonus oploopt; hogere waarde = sneller agressief.")]
    [SerializeField] private float inWaveAggressionCurve = 1.8f;

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

    /// <summary>
    /// Runtime aggressiebonus (0–1+) gebaseerd op kills, wave-voortgang en wave-nummer.
    /// Geesten lezen dit elke frame om hun gedrag live aan te passen.
    /// </summary>
    public float DynamicAggressionBonus
    {
        get
        {
            // Kill-bonus: hoe meer de speler gedood heeft, hoe agressiever de geesten
            float killBonus = Mathf.Clamp01((float)TotalKillCount / Mathf.Max(1f, killsForMaxAggressionBonus))
                            * maxKillAggressionBonus
                            * DifficultySettings.KillAggressionMultiplier;

            // Wave-progress-bonus: geesten worden slimmer naarmate de wave langer duurt
            // Hogere waves laten de bonus sneller oplopen (inWaveAggressionCurve)
            float waveFraction = _currentWaveDuration > 0f
                ? Mathf.Clamp01(_waveElapsedTime / _currentWaveDuration)
                : 0f;
            float waveLevelScale = 1f + (CurrentWave - 1) * 0.12f;
            float waveProgressBonus = Mathf.Pow(waveFraction, 1f / inWaveAggressionCurve)
                                    * maxInWaveAggressionBonus
                                    * waveLevelScale;

            return killBonus + waveProgressBonus;
        }
    }

    /// <summary>Eén hint-positie (eerste richting). Bestaande code-compatibiliteit.</summary>
    public Vector3 NextWaveHintPosition => NextWaveHintPositions.Count > 0
        ? NextWaveHintPositions[0] : Vector3.zero;

    /// <summary>Hint-posities voor alle spawn-richtingen van de volgende wave.</summary>
    public List<Vector3> NextWaveHintPositions
    {
        get
        {
            float mapRadius = MapController.Instance != null
                ? MapController.Instance.CurrentRadius - MapController.Instance.HardWallInset
                : 40f;
            Vector3 center = MapController.Instance != null
                ? MapController.Instance.PlatformCenter
                : Vector3.zero;

            var result = new List<Vector3>();
            var dirs = _hasPrerolledDirection && _prerolledSpawnDirections.Count > 0
                ? _prerolledSpawnDirections
                : new List<Vector3> { Vector3.forward };
            foreach (var dir in dirs)
                result.Add(center + dir.normalized * mapRadius);
            return result;
        }
    }

    // Wave-clear tracking
    private int _waveKillCount;
    private int _waveTotalSpawned;
    private int _totalWaveEnemies;
    private float _currentWaveDuration;
    private float _pendingPenalty;
    private int _waveSurvivorCount;
    private float _waveElapsedTime;

    private EnemySpawner spawner;
    private PickupSpawner pickupSpawner;
    private WaveUIController waveUI;
    private bool difficultyApplied;
    private float scoutTimer;
    private int wavesSinceLastSpecial = 0;
    private readonly List<Vector3> _prerolledSpawnDirections = new List<Vector3>();
    private bool _hasPrerolledDirection;

    // Gemak-property zodat multiplier-methodes schoon blijven
    private float ClearPenaltyMult => 1f + _pendingPenalty;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>Aanroepen vanuit PowerUpSpawner zodra alle power-ups zijn opgepakt.</summary>
    public void OnAllPowerUpsCollected()
    {
        if (!IsBreak) return;
        TimeRemaining = Mathf.Min(TimeRemaining, 5f);
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
    /// <param name="countForCombo">False als de kill niet voor de combo-meter telt (bv. collision-hit).</param>
    public void NotifyEnemyKilled(bool countForCombo = true)
    {
        TotalKillCount++;
        if (IsBreak) return;
        _waveKillCount++;
        int displayTotal = (_totalWaveEnemies > 0 && _totalWaveEnemies < int.MaxValue)
            ? _totalWaveEnemies : _waveTotalSpawned;
        clearUI?.UpdateProgress(_waveKillCount, displayTotal);
        if (countForCombo)
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
        pickupSpawner = FindObjectOfType<PickupSpawner>();
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
        if (!IsBreak)
            _waveElapsedTime += Time.deltaTime;

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

        // Reset kill-teller en wave-timer voor deze wave
        _waveKillCount    = 0;
        _waveTotalSpawned = 0;
        _waveElapsedTime  = 0f;

        CurrentWaveType = DetermineWaveType();

        // Bepaal spawn richtingen (gebruik prerolled richtingen als beschikbaar)
        List<Vector3> spawnDirections;
        if (_hasPrerolledDirection && _prerolledSpawnDirections.Count > 0)
            spawnDirections = new List<Vector3>(_prerolledSpawnDirections);
        else
            spawnDirections = new List<Vector3> { GetWaveSpawnDirection() };
        _hasPrerolledDirection = false;
        CurrentWaveSpawnDirection = spawnDirections[0];

        float diffScale = DifficultySettings.AggressionScaleMultiplier;
        AggressionLevel = Mathf.Clamp01((CurrentWave - 1) * aggressionPerWave * diffScale);

        int maxEnemies = GetWaveMaxEnemies();
        _totalWaveEnemies = CurrentWaveType == WaveType.Siege ? int.MaxValue : maxEnemies;
        float effectiveGroupDelay = CurrentWaveType == WaveType.Siege ? 0f
            : Mathf.Max(0f, interGroupDelay + Random.Range(-interGroupDelayVariance, interGroupDelayVariance));
        _currentWaveDuration = GetWaveDuration(maxEnemies, spawnDirections.Count, effectiveGroupDelay);
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
        pickupSpawner?.Reshuffle();
        if (powerUpModifier < 0.99f)
            progressUI?.ShowPowerUpFailText(powerUpModifier);

        spawner?.OnWaveStarted(CurrentWave, AggressionLevel, maxEnemies, spawnInterval,
                                CurrentWaveType, enemyHealthMultiplier, enemySpeedMultiplier, spawnCap,
                                spawnDirections, effectiveGroupDelay);
        waveUI?.ShowWaveMessage(CurrentWave, CurrentWaveType);
    }

    private void PrerollNextWaveDirection()
    {
        _prerolledSpawnDirections.Clear();

        int groupCount = Random.Range(minGroupCount, maxGroupCount + 1);
        groupCount = Mathf.Max(1, groupCount);

        float baseAngle = Random.Range(0f, 360f);
        float spread = 360f / groupCount;

        for (int i = 0; i < groupCount; i++)
        {
            Vector3 dir;
            if (!useRandomDirection && waveSpawnPoints != null && waveSpawnPoints.Length > 0)
            {
                // Gebruik vaste spawn-punten als basis maar roteer ze per groep
                int index = ((CurrentWave + i) % waveSpawnPoints.Length);
                Vector3 pointPos = waveSpawnPoints[index].position;
                Vector3 center = MapController.Instance != null
                    ? MapController.Instance.PlatformCenter : Vector3.zero;
                dir = (pointPos - center).normalized;
            }
            else
            {
                float angle = baseAngle + i * spread + Random.Range(-18f, 18f);
                dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            }
            _prerolledSpawnDirections.Add(dir);
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
        float baseDur = CurrentWave == 1 ? firstBreakDuration : breakDuration;
        TimeRemaining = baseDur * DifficultySettings.BreakDurationMultiplier;
        scoutTimer = scoutSpawnInterval * 0.5f;
        spawner?.OnWaveBreak(); // CleanupAllEnemies wordt hierin aangeroepen → stelt LastWaveSurvivorCount in
        _waveSurvivorCount = spawner != null ? spawner.LastWaveSurvivorCount : 0;

        PrerollNextWaveDirection();
        PowerUpSpawner.Instance?.OnBreakStarted(NextWaveHintPositions);

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
            _pendingPenalty = missedFraction * maxClearPenalty * DifficultySettings.ClearPenaltyMultiplier;

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
        int count = Mathf.RoundToInt(baseMaxEnemies * Mathf.Pow(enemyGrowthFactor, CurrentWave - 1));

        if (CurrentWaveType == WaveType.Horde)
            count = Mathf.RoundToInt(count * 1.8f);
        else if (CurrentWaveType == WaveType.Elite)
            count = Mathf.RoundToInt(count * 0.5f);

        count = Mathf.RoundToInt(count * mult * ClearPenaltyMult);

        // Elke overlevende van de vorige wave voegt een extra enemy toe
        count += _waveSurvivorCount;

        return Mathf.Min(count, maxEnemyLimit);
    }

    private float GetWaveDuration(int enemyCount, int groupCount = 1, float groupDelay = 0f)
    {
        if (CurrentWaveType == WaveType.Siege)
            return baseWaveDuration;
        float duration = baseWaveDuration + waveDurationPerEnemy * enemyCount;
        // Elke extra groepsvertraging verlengt de wave zodat alle groepen gespawnd worden
        duration += Mathf.Max(0, groupCount - 1) * groupDelay;
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