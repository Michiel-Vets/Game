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

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);

        if (enemyPrefab == null)
            Debug.LogError("EnemySpawner: Enemy Prefab is not assigned.");

        BakeEdgePoints();
    }

    private void Update()
    {
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

    // Bake a list of ground-edge positions once at startup.
    // Strategy: for each angle, march outward from the map center
    // until the ground disappears, then step back one unit.
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
                    // Ground ended — step back slightly to stay on the edge
                    if (foundAny)
                    {
                        Vector3 edgePoint = lastValidPoint - dir * edgeInsetDistance;
                        edgePoints.Add(edgePoint + Vector3.up * spawnYOffset);
                    }
                    break;
                }
            }

            // If ground went all the way to the scan radius, use the last valid point
            if (foundAny && edgePoints.Count == i)
                edgePoints.Add(lastValidPoint + Vector3.up * spawnYOffset);
        }

        if (edgePoints.Count == 0)
            Debug.LogWarning("EnemySpawner: No edge points found. Check groundLayer and edgeScanRadius.");
    }

    private float GetCurrentSpawnInterval()
    {
        return Mathf.Max(minimumSpawnInterval, startSpawnInterval - survivedTime * spawnIntervalDecreasePerSecond);
    }

    private int GetCurrentMaxEnemiesAlive()
    {
        int increases = Mathf.FloorToInt(survivedTime / increaseLimitEverySeconds);
        return Mathf.Min(startMaxEnemiesAlive + increases * enemyLimitIncreaseAmount, maxEnemyLimit);
    }

    private void SpawnEnemies()
    {
        int currentMax = GetCurrentMaxEnemiesAlive();

        for (int i = 0; i < enemiesPerSpawn; i++)
        {
            if (activeEnemies.Count >= currentMax)
                return;

            if (!TryGetEdgeSpawnPosition(out Vector3 spawnPosition))
                return;

            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            if (enemy.TryGetComponent(out EnemyController controller))
                controller.SetTarget(player);

            activeEnemies.Add(enemy);
        }
    }

    private bool TryGetEdgeSpawnPosition(out Vector3 spawnPosition)
    {
        if (edgePoints.Count == 0)
        {
            spawnPosition = Vector3.zero;
            return false;
        }

        // Try a few random edge points, pick one furthest from the player
        // so enemies don't spawn directly on top of the player
        int tries = Mathf.Min(5, edgePoints.Count);
        Vector3 best = edgePoints[Random.Range(0, edgePoints.Count)];
        float bestDist = Vector3.Distance(best, player.position);

        for (int i = 1; i < tries; i++)
        {
            Vector3 candidate = edgePoints[Random.Range(0, edgePoints.Count)];
            float dist = Vector3.Distance(candidate, player.position);

            if (dist > bestDist)
            {
                best = candidate;
                bestDist = dist;
            }
        }

        spawnPosition = best;
        return true;
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
            Gizmos.DrawSphere(p, 0.4f);
    }
}