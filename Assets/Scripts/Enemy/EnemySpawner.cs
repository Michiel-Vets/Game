using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform player;

    [Header("Difficulty Over Time")]
    [SerializeField] private float startSpawnInterval = 4f;
    [SerializeField] private float minimumSpawnInterval = 0.5f;
    [SerializeField] private float spawnIntervalDecreasePerSecond = 0.03f;
    [SerializeField] private int startMaxEnemiesAlive = 5;
    [SerializeField] private int maxEnemyLimit = 50;
    [SerializeField] private float increaseLimitEverySeconds = 10f;
    [SerializeField] private int enemyLimitIncreaseAmount = 2;

    [Header("Spawn Amount")]
    [SerializeField] private int enemiesPerSpawn = 1;

    [Header("Map Edge Detection")]
    [SerializeField] private float edgeScanRadius = 60f;
    [SerializeField] private float edgeScanStep = 5f;
    [SerializeField] private float edgeInsetDistance = 1f;
    [SerializeField] private int edgeSampleAngles = 36;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 1f;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<Vector3> edgePoints = new List<Vector3>();

    private float survivedTime;
    private float spawnTimer;
    private bool difficultyApplied;

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);

        if (enemyPrefab == null)
            Debug.LogError("EnemySpawner: Enemy Prefab is not assigned.");

        BakeEdgePoints();
    }

    private void ApplyDifficulty()
    {
        DifficultySettings.Load();
        startSpawnInterval *= DifficultySettings.SpawnIntervalMultiplier;
        minimumSpawnInterval *= DifficultySettings.SpawnIntervalMultiplier;
        startMaxEnemiesAlive = Mathf.Max(1, startMaxEnemiesAlive + DifficultySettings.MaxEnemiesBonus);
        difficultyApplied = true;
    }

    private void Update()
    {
        if (!difficultyApplied)
        {
            if (Time.timeScale <= 0f) return;
            ApplyDifficulty();
        }

        PlayerFinder.TryAssignIfNull(ref player);

        if (enemyPrefab == null || player == null)
            return;

        CleanupDestroyedEnemies();

        survivedTime += Time.deltaTime;
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= GetCurrentSpawnInterval())
        {
            spawnTimer = 0f;
            SpawnEnemies();
        }
    }

    private void BakeEdgePoints()
    {
        edgePoints.Clear();

        Vector3 center = transform.position;

        for (int i = 0; i < edgeSampleAngles; i++)
        {
            float angle = i * (360f / edgeSampleAngles);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            Vector3 lastValidPoint = Vector3.zero;
            bool foundAny = false;

            for (float dist = edgeScanStep; dist <= edgeScanRadius; dist += edgeScanStep)
            {
                Vector3 sample = center + dir * dist;
                Vector3 rayOrigin = sample + Vector3.up * raycastHeight;

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                    raycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
                {
                    lastValidPoint = hit.point;
                    foundAny = true;
                }
                else
                {
                    if (foundAny)
                    {
                        Vector3 edgePoint = lastValidPoint - dir * edgeInsetDistance;
                        edgePoints.Add(edgePoint + Vector3.up * spawnYOffset);
                    }
                    break;
                }
            }

            if (foundAny && edgePoints.Count == i)
                edgePoints.Add(lastValidPoint + Vector3.up * spawnYOffset);
        }

        if (edgePoints.Count == 0)
            Debug.LogWarning("EnemySpawner: No edge points found. Check groundLayer and edgeScanRadius.");
    }

    private float GetCurrentSpawnInterval()
    {
        float interval = startSpawnInterval - spawnIntervalDecreasePerSecond * survivedTime;
        return Mathf.Max(interval, minimumSpawnInterval);
    }

    private int GetCurrentMaxEnemies()
    {
        int bonus = Mathf.FloorToInt(survivedTime / increaseLimitEverySeconds) * enemyLimitIncreaseAmount;
        return Mathf.Min(startMaxEnemiesAlive + bonus, maxEnemyLimit);
    }

    private void SpawnEnemies()
    {
        if (activeEnemies.Count >= GetCurrentMaxEnemies()) return;
        if (edgePoints.Count == 0) return;

        for (int i = 0; i < enemiesPerSpawn; i++)
        {
            if (activeEnemies.Count >= GetCurrentMaxEnemies()) break;

            Vector3 spawnPos = edgePoints[Random.Range(0, edgePoints.Count)];
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null)
                controller.SetSurvivedTime(survivedTime);

            activeEnemies.Add(enemy);
        }
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (Vector3 p in edgePoints)
            Gizmos.DrawSphere(p, 0.5f);
    }
}