using UnityEngine;

public enum DifficultyLevel { Easy, Normal, Hard }

public static class DifficultySettings
{
    public static DifficultyLevel Current { get; set; } = DifficultyLevel.Normal;

    // Hoe sneller enemies spawnen (lager = sneller)
    public static float SpawnIntervalMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 1.4f,
        DifficultyLevel.Hard   => 0.38f,
        _                      => 0.68f,   // Normal: 32% sneller dan origineel
    };

    public static int MaxEnemiesBonus => Current switch
    {
        DifficultyLevel.Easy   => -2,
        DifficultyLevel.Hard   => 5,
        _                      => 2,
    };

    public static float EnemySpeedMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.72f,
        DifficultyLevel.Hard   => 1.65f,
        _                      => 1.22f,
    };

    public static float EnemyHealthMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.55f,
        DifficultyLevel.Hard   => 2.4f,
        _                      => 1.35f,
    };

    // Meer pickups op Easy, minder op Hard
    public static float PickupMaxMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 1.6f,
        DifficultyLevel.Hard   => 0.6f,
        _                      => 1f,
    };

    public static float PickupIntervalMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.55f,
        DifficultyLevel.Hard   => 1.6f,   // minder frequent op Hard
        _                      => 1.1f,
    };

    public static float AggressionScaleMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.45f,
        DifficultyLevel.Hard   => 2.8f,
        _                      => 1.45f,
    };

    public static float BreakDurationMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 1.6f,
        DifficultyLevel.Hard   => 0.42f,
        _                      => 0.80f,   // Normal: kortere breaks
    };

    public static float WaveEnemyCountMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.60f,
        DifficultyLevel.Hard   => 2.10f,
        _                      => 1.30f,
    };

    // Hoe snel scouts spawnen tijdens break (lager = sneller)
    public static float ScoutSpawnIntervalMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 1.6f,
        DifficultyLevel.Hard   => 0.32f,
        _                      => 0.75f,
    };

    // Hoe zwaar de straf is voor een niet-geclearde wave (hoger = zwaardere straf)
    public static float ClearPenaltyMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.4f,
        DifficultyLevel.Hard   => 2.2f,
        _                      => 1f,
    };

    // Hoe snel enemies healen (hoger = sneller healen)
    public static float EnemyHealSpeedMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.6f,
        DifficultyLevel.Hard   => 2.5f,
        _                      => 1f,
    };

    // Hoe hard kills de dynamische aggression opschalen (hogere moeilijkheid = geesten worden sneller agressiever)
    public static float KillAggressionMultiplier => Current switch
    {
        DifficultyLevel.Easy   => 0.30f,
        DifficultyLevel.Hard   => 2.00f,
        _                      => 0.80f,
    };

    public static int SpecialWaveIntervalBonus => Current switch
    {
        DifficultyLevel.Easy   => 2,
        DifficultyLevel.Hard   => -1,
        _                      => 0,
    };

    public static float SpecialWaveMixChanceBonus => Current switch
    {
        DifficultyLevel.Easy   => -0.2f,
        DifficultyLevel.Hard   => 0.4f,
        _                      => 0.1f,
    };

    public static void Load()
    {
        Current = (DifficultyLevel)PlayerPrefs.GetInt("Difficulty", 1);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("Difficulty", (int)Current);
        PlayerPrefs.Save();
    }
}
