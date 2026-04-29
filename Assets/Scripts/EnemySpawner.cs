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

    [Header("Spawn Distance")]
    [SerializeField] private float minSpawnDistance = 12f;
    [SerializeField] private float maxSpawnDistance = 18f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 1f;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    private float survivedTime;
    private float spawnTimer;

    private void Start()
    {
        FindPlayerIfNeeded();

        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: Enemy Prefab is not assigned.");
        }
    }

    private void Update()
    {
        FindPlayerIfNeeded();

        if (enemyPrefab == null || player == null)
            return;

        CleanupDestroyedEnemies();

        survivedTime += Time.deltaTime;
        spawnTimer += Time.deltaTime;

        float currentSpawnInterval = GetCurrentSpawnInterval();

        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemies();
        }
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
            return;

        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");

        if (foundPlayer != null)
            player = foundPlayer.transform;
    }

    private float GetCurrentSpawnInterval()
    {
        float interval = startSpawnInterval - survivedTime * spawnIntervalDecreasePerSecond;
        return Mathf.Max(minimumSpawnInterval, interval);
    }

    private int GetCurrentMaxEnemiesAlive()
    {
        int increases = Mathf.FloorToInt(survivedTime / increaseLimitEverySeconds);
        int currentLimit = startMaxEnemiesAlive + increases * enemyLimitIncreaseAmount;

        return Mathf.Min(currentLimit, maxEnemyLimit);
    }

    private void SpawnEnemies()
    {
        int currentMaxEnemiesAlive = GetCurrentMaxEnemiesAlive();

        for (int i = 0; i < enemiesPerSpawn; i++)
        {
            if (activeEnemies.Count >= currentMaxEnemiesAlive)
                return;

            if (!TryGetSpawnPosition(out Vector3 spawnPosition))
                return;

            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            EnemyController enemyController = enemy.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                enemyController.SetTarget(player);
            }

            activeEnemies.Add(enemy);
        }
    }

    private bool TryGetSpawnPosition(out Vector3 spawnPosition)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 randomDirection = Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude < 0.01f)
                randomDirection = Vector2.right;

            randomDirection.Normalize();

            float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);

            Vector3 horizontalOffset = new Vector3(
                randomDirection.x,
                0f,
                randomDirection.y
            ) * randomDistance;

            Vector3 rayStart = player.position + horizontalOffset + Vector3.up * raycastHeight;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayer))
            {
                spawnPosition = hit.point + Vector3.up * spawnYOffset;
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }
}