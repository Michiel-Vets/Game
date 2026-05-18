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
