using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform player;

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

    private float spawnTimer;
    private float currentSpawnInterval = 8f;
    private int currentMaxEnemies;
    private float currentAggressionLevel;
    private int currentWaveNumber;
    private bool isActive;

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);

        if (enemyPrefab == null)
            Debug.LogError("EnemySpawner: Enemy Prefab is not assigned.");

        BakeEdgePoints();
    }

    public void OnWaveStarted(int wave, float aggression, int maxEnemies, float spawnInterval)
    {
        currentWaveNumber = wave;
        currentAggressionLevel = aggression;
        currentMaxEnemies = maxEnemies;
        currentSpawnInterval = spawnInterval;
        isActive = true;
        spawnTimer = 0f;
    }

    public void OnWaveBreak()
    {
        isActive = false;
    }

    private void Update()
    {
        PlayerFinder.TryAssignIfNull(ref player);

        if (enemyPrefab == null || player == null || !isActive) return;

        CleanupDestroyedEnemies();

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemies();
        }
    }

    private void SpawnEnemies()
    {
        if (activeEnemies.Count >= currentMaxEnemies) return;
        if (edgePoints.Count == 0) return;

        Vector3 spawnPos = edgePoints[Random.Range(0, edgePoints.Count)];
        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
            controller.SetWaveData(currentWaveNumber, currentAggressionLevel);

        activeEnemies.Add(enemy);
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