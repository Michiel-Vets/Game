using UnityEngine;

public enum DifficultyLevel { Easy, Normal, Hard }

public static class DifficultySettings
{
    public static DifficultyLevel Current { get; set; } = DifficultyLevel.Normal;

    public static float SpawnIntervalMultiplier => Current switch
    {
        DifficultyLevel.Easy => 1.6f,
        DifficultyLevel.Hard => 0.55f,
        _ => 1f,
    };

    public static int MaxEnemiesBonus => Current switch
    {
        DifficultyLevel.Easy => -2,
        DifficultyLevel.Hard => 3,
        _ => 0,
    };

    public static float EnemySpeedMultiplier => Current switch
    {
        DifficultyLevel.Easy => 0.75f,
        DifficultyLevel.Hard => 1.35f,
        _ => 1f,
    };

    public static float EnemyHealthMultiplier => Current switch
    {
        DifficultyLevel.Easy => 0.6f,
        DifficultyLevel.Hard => 1.8f,
        _ => 1f,
    };

    public static float PickupMaxMultiplier => Current switch
    {
        DifficultyLevel.Easy => 1.5f,
        DifficultyLevel.Hard => 0.5f,
        _ => 1f,
    };

    public static float PickupIntervalMultiplier => Current switch
    {
        DifficultyLevel.Easy => 0.6f,
        DifficultyLevel.Hard => 2.0f,
        _ => 1f,
    };

    public static float AggressionScaleMultiplier => Current switch
    {
        DifficultyLevel.Easy => 0.5f,
        DifficultyLevel.Hard => 2.0f,
        _ => 1f,
    };

    public static float BreakDurationMultiplier => Current switch
    {
        DifficultyLevel.Easy => 1.5f,
        DifficultyLevel.Hard => 0.6f,
        _ => 1f,
    };

    public static float WaveEnemyCountMultiplier => Current switch
    {
        DifficultyLevel.Easy => 0.6f,
        DifficultyLevel.Hard => 1.5f,
        _ => 1f,
    };

    public static int SpecialWaveIntervalBonus => Current switch
    {
        DifficultyLevel.Easy => 2,     // Minder speciale waves
        DifficultyLevel.Hard => -1,    // Meer speciale waves
        _ => 0,
    };

    public static float ScoutSpawnIntervalMultiplier => Current switch
    {
        DifficultyLevel.Easy => 1.5f,  // Langzamer scouts
        DifficultyLevel.Hard => 0.5f,  // Sneller scouts
        _ => 1f,
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