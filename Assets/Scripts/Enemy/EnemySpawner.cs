using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject scoutEnemyPrefab;
    [SerializeField] private GameObject eliteEnemyPrefab;

    [Header("Map Edge Detection")]
    [SerializeField] private float edgeScanRadius = 60f;
    [SerializeField] private float edgeScanStep = 5f;
    [SerializeField] private float edgeInsetDistance = 1f;
    [SerializeField] private int edgeSampleAngles = 36;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 1f;

    [Header("Directional Spawning")]
    [SerializeField, Range(0f, 1f)] private float directionalSpawnFraction = 0.7f;
    [SerializeField] private float directionalArcDegrees = 60f;

    [Header("Surge")]
    [SerializeField, Range(0f, 1f)] private float surgeTriggerFraction = 0.4f;
    [SerializeField] private int surgeCount = 4;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<Vector3> edgePoints = new List<Vector3>();

    private float spawnTimer;
    private float currentSpawnInterval;
    private int currentMaxEnemies;
    private float currentAggressionLevel;
    private int currentWaveNumber;
    private bool isActive;
    private bool isSiegeMode;
    private bool isEliteMode;
    private float currentAttackDirection;
    private int regularSpawned;
    private bool surgeTriggered;

    private Transform player;

    public bool AllEnemiesDead
    {
        get
        {
            if (!isActive || isSiegeMode) return false;
            CleanupDestroyedEnemies();
            return regularSpawned >= currentMaxEnemies && activeEnemies.Count == 0;
        }
    }

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);

        if (enemyPrefab == null)
            Debug.LogError("EnemySpawner: Enemy Prefab is not assigned.");

        BakeEdgePoints();
    }

    public void OnWaveStarted(int wave, float aggression, int maxEnemies, float spawnInterval,
        bool siege, bool elite, float attackDirection)
    {
        currentWaveNumber = wave;
        currentAggressionLevel = aggression;
        currentMaxEnemies = maxEnemies;
        currentSpawnInterval = spawnInterval;
        isSiegeMode = siege;
        isEliteMode = elite;
        currentAttackDirection = attackDirection;
        isActive = true;
        spawnTimer = 0f;
        regularSpawned = 0;
        surgeTriggered = false;
    }

    public void OnWaveBreak()
    {
        isActive = false;
    }

    public void SpawnScouts(int count, float aggression)
    {
        if (edgePoints.Count == 0) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = edgePoints[Random.Range(0, edgePoints.Count)];
            GameObject prefab = scoutEnemyPrefab != null ? scoutEnemyPrefab : enemyPrefab;
            SpawnSingleEnemy(prefab, pos, currentWaveNumber, aggression * 0.5f, scout: true);
        }
    }

    private void Update()
    {
        PlayerFinder.TryAssignIfNull(ref player);
        if (player == null || !isActive) return;

        CleanupDestroyedEnemies();

        bool shouldSpawn = isSiegeMode || regularSpawned < currentMaxEnemies;
        if (!shouldSpawn) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnRegularEnemy();
        }

        if (!surgeTriggered && !isSiegeMode &&
            regularSpawned >= Mathf.RoundToInt(currentMaxEnemies * surgeTriggerFraction) &&
            regularSpawned > 0)
        {
            surgeTriggered = true;
            TriggerSurge();
        }
    }

    private void SpawnRegularEnemy()
    {
        Vector3 pos = GetSpawnPosition(useDirectional: true);
        GameObject prefab = isEliteMode && eliteEnemyPrefab != null ? eliteEnemyPrefab : enemyPrefab;
        SpawnSingleEnemy(prefab, pos, currentWaveNumber, currentAggressionLevel, elite: isEliteMode);
        regularSpawned++;
    }

    private void TriggerSurge()
    {
        EnemyController.BroadcastGroupAttack();

        int count = Mathf.Min(surgeCount, currentMaxEnemies / 2 + 1);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetDirectionalSpawnPosition(currentAttackDirection, narrow: true);
            SpawnSingleEnemy(enemyPrefab, pos, currentWaveNumber, currentAggressionLevel);
        }
    }

    private void SpawnSingleEnemy(GameObject prefab, Vector3 pos, int wave, float aggression,
        bool scout = false, bool elite = false)
    {
        if (prefab == null) return;
        GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);
        activeEnemies.Add(enemy);

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller == null) return;

        controller.SetWaveData(wave, aggression);
        if (scout) controller.SetScoutMode();
        else if (elite) controller.SetEliteMode();
    }

    private Vector3 GetSpawnPosition(bool useDirectional)
    {
        if (edgePoints.Count == 0) return transform.position + Vector3.up;

        bool dir = useDirectional && Random.value <= directionalSpawnFraction;
        return dir
            ? GetDirectionalSpawnPosition(currentAttackDirection, narrow: false)
            : edgePoints[Random.Range(0, edgePoints.Count)];
    }

    private Vector3 GetDirectionalSpawnPosition(float directionAngle, bool narrow)
    {
        if (edgePoints.Count == 0) return transform.position + Vector3.up;

        float arc = narrow ? directionalArcDegrees * 0.4f : directionalArcDegrees;
        float offset = Random.Range(-arc * 0.5f, arc * 0.5f);
        Vector3 targetDir = Quaternion.Euler(0f, directionAngle + offset, 0f) * Vector3.forward;
        Vector3 center = transform.position;

        Vector3 best = edgePoints[0];
        float bestDot = -2f;
        foreach (Vector3 p in edgePoints)
        {
            float dot = Vector3.Dot((p - center).normalized, targetDir);
            if (dot > bestDot) { bestDot = dot; best = p; }
        }
        return best;
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