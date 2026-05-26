using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    private enum EnemyVariant { Normal, Elite, Scout }

    [Header("References")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform player;

    [Header("Map Edge Detection")]
    [SerializeField] private float edgeScanRadius = 60f;
    [SerializeField] private float edgeScanStep = 5f;
    [SerializeField] private float edgeInsetDistance = 1f;
    [SerializeField] private int edgeSampleAngles = 36;

    [Header("Map Scaling")]
    [Tooltip("Hoe ver de map maximaal kan groeien t.o.v. de startgrootte (0 = geen groei, 1 = 2× startgrootte).")]
    [SerializeField] private float mapGrowthScale = 0.5f;
    [SerializeField] private float maxEdgeScanRadius = 90f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 1f;

    [Header("Circular Map Spawning")]
    [Tooltip("Hoeveel meter buiten de map-rand vijanden spawnen (boven de afgrond).")]
    [SerializeField] private float spawnBeyondEdge = 30f;
    [Tooltip("Maximale willekeurige spreiding in graden per vijand (voor groepsspreiding).")]
    [SerializeField] private float spawnAngleSpread = 20f;

    [Header("Elite Enemies (Normal Waves)")]
    [SerializeField] private float eliteChanceBase = 0.05f;
    [SerializeField] private float eliteChancePerWave = 0.02f;
    [SerializeField] private float eliteChanceMax = 0.30f;

    [Header("Scout Enemies (Normal Waves)")]
    [SerializeField] private float scoutChanceBase = 0.10f;
    [SerializeField] private float scoutChancePerWave = 0.015f;
    [SerializeField] private float scoutChanceMax = 0.35f;

    [Header("Scouts During Break")]
    [SerializeField] private GameObject scoutPrefab;

    [Header("Group Spawning (Non-Siege Waves)")]
    [Tooltip("Aantal enemies per groep.")]
    [SerializeField] private int enemiesPerGroup = 4;
    [Tooltip("Seconden tussen groepen.")]
    [SerializeField] private float groupSpawnInterval = 8f;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<Vector3> edgePoints = new List<Vector3>();

    private float _baseEdgeScanRadius;

    private float spawnTimer;
    private float currentSpawnInterval;
    private int currentMaxEnemies;
    private float currentAggressionLevel;
    private int currentWaveNumber;
    private WaveType currentWaveType;
    private float currentEnemyHealthMultiplier = 1f;
    private float currentEnemySpeedMultiplier = 1f;
    private int currentSpawnCap;
    private Vector3 currentSpawnDirection;
    private bool isActive;
    private bool isBreak;

    // Groepsspawn state
    private int _remainingToSpawn;
    private float _groupTimer;

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);
        if (enemyPrefab == null)
            Debug.LogError("EnemySpawner: Enemy Prefab is not assigned.");
        _baseEdgeScanRadius = edgeScanRadius;
        BakeEdgePoints();
    }

    public void OnWaveStarted(int wave, float aggression, int maxEnemies, float spawnInterval,
                              WaveType waveType, float healthMult, float speedMult, int spawnCap,
                              Vector3 spawnDirection)
    {
        currentWaveNumber = wave;
        currentAggressionLevel = aggression;
        currentMaxEnemies = maxEnemies;
        currentSpawnInterval = spawnInterval;
        currentWaveType = waveType;
        currentEnemyHealthMultiplier = healthMult;
        currentEnemySpeedMultiplier = speedMult;
        currentSpawnCap = spawnCap;
        currentSpawnDirection = spawnDirection;
        isBreak = false;
        spawnTimer = 0f;

        // Map meeschalen met enemy count
        UpdateMapRadius(maxEnemies);

        CleanupAllEnemies();

        if (currentWaveType == WaveType.Siege)
        {
            // Siege: timer-based spawning zoals voorheen
            isActive = true;
        }
        else
        {
            // Andere waves: groepen spawnen doorheen de wave
            _remainingToSpawn = currentSpawnCap;
            _groupTimer = 0f; // eerste groep meteen
            isActive = true;
        }
    }

    public void OnWaveBreak()
    {
        isActive = false;
        isBreak = true;
    }

    public void SpawnScout()
    {
        if (!isBreak) return;
        if (activeEnemies.Count >= 3) return;

        Vector3 spawnPos;
        if (MapController.Instance != null)
        {
            // Circulaire map: willekeurige richting, buiten de rand
            float angle = Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float radius = MapController.Instance.CurrentRadius + spawnBeyondEdge;
            spawnPos = transform.position + dir * radius;
            spawnPos.y = MapController.Instance.SurfaceY + spawnYOffset;
        }
        else
        {
            if (edgePoints.Count == 0) return;
            spawnPos = edgePoints[Random.Range(0, edgePoints.Count)];
        }
        GameObject enemy = Instantiate(scoutPrefab != null ? scoutPrefab : enemyPrefab, spawnPos, Quaternion.identity);

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.SetWaveData(currentWaveNumber, 0.1f);
            controller.SetScoutMode();

            ScoutDropReward reward = enemy.GetComponent<ScoutDropReward>()
                                  ?? enemy.AddComponent<ScoutDropReward>();
            reward.Setup();
        }

        activeEnemies.Add(enemy);
    }

    private void Update()
    {
        PlayerFinder.TryAssignIfNull(ref player);
        if (enemyPrefab == null || player == null) return;

        CleanupDestroyedEnemies();

        if (!isActive) return;

        if (currentWaveType == WaveType.Siege)
        {
            // Siege: één enemy tegelijk op interval
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= currentSpawnInterval)
            {
                spawnTimer = 0f;
                if (activeEnemies.Count < currentSpawnCap)
                    SpawnSingleEnemy(currentSpawnDirection);
            }
        }
        else
        {
            // Andere waves: groepen op interval
            if (_remainingToSpawn <= 0) return;

            _groupTimer -= Time.deltaTime;
            if (_groupTimer <= 0f)
                SpawnGroup();
        }
    }

    // ── Groepsspawn ───────────────────────────────────────────────────────────

    private void SpawnGroup()
    {
        // Elke groep uit een andere willekeurige richting
        float angle = Random.Range(0f, 360f);
        Vector3 groupDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

        int toSpawn = Mathf.Min(enemiesPerGroup, _remainingToSpawn);
        for (int i = 0; i < toSpawn; i++)
        {
            if (activeEnemies.Count >= currentSpawnCap) break;
            SpawnSingleEnemy(groupDir);
            _remainingToSpawn--;
        }

        _groupTimer = groupSpawnInterval;

        if (_remainingToSpawn <= 0)
            isActive = false;
    }

    private void SpawnSingleEnemy(Vector3 direction)
    {
        // Bij circulaire map: spawn buiten de rand; anders: gebruik edge-punten
        Vector3 spawnPos;
        if (MapController.Instance != null)
            spawnPos = GetSpawnPositionCircular(direction);
        else
        {
            if (edgePoints.Count == 0) return;
            spawnPos = GetSpawnPositionInDirection(direction);
        }
        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.SetWaveData(currentWaveNumber, currentAggressionLevel);

            if (currentWaveType == WaveType.Horde)
                controller.SetHordeMode();
            else if (currentWaveType == WaveType.Elite)
                controller.SetEliteMode();

            bool isVisible = controller.IsInAttackMode();
            controller.ApplyMultipliers(currentEnemyHealthMultiplier, currentEnemySpeedMultiplier, isVisible);

            ApplyVariant(controller);
        }

        activeEnemies.Add(enemy);
        WaveManager.Instance?.NotifyEnemySpawned();
    }

    // ── Map radius schalen ────────────────────────────────────────────────────

    private void UpdateMapRadius(int enemyCount)
    {
        // Als MapController actief is, regelt die de schaling via wave-nummer.
        if (MapController.Instance != null) return;

        if (enemyCount <= 0) return;

        float t = Mathf.Clamp01((float)enemyCount / 50f) * mapGrowthScale;
        float newRadius = Mathf.Lerp(_baseEdgeScanRadius, maxEdgeScanRadius, t);
        newRadius = Mathf.Clamp(newRadius, _baseEdgeScanRadius, maxEdgeScanRadius);

        if (Mathf.Abs(newRadius - edgeScanRadius) > 1f)
        {
            edgeScanRadius = newRadius;
            BakeEdgePoints();

            // Mist world size meeschalen
            float mistSize = newRadius * 2f;
            MistTrailController.Instance?.SetWorldSize(mistSize);
        }
    }

    // ── Variant ───────────────────────────────────────────────────────────────

    private void ApplyVariant(EnemyController controller)
    {
        EnemyVariant variant = RollVariant();
        switch (variant)
        {
            case EnemyVariant.Elite:
                controller.SetEliteMode();
                break;
            case EnemyVariant.Scout:
                controller.SetScoutMode();
                break;
        }
    }

    private EnemyVariant RollVariant()
    {
        float eliteChance = Mathf.Min(
            eliteChanceBase + eliteChancePerWave * (currentWaveNumber - 1), eliteChanceMax);
        float scoutChance = Mathf.Min(
            scoutChanceBase + scoutChancePerWave * (currentWaveNumber - 1), scoutChanceMax);

        if (currentWaveType == WaveType.Horde)
            scoutChance *= 1.5f;
        else if (currentWaveType == WaveType.Elite)
            eliteChance *= 2f;

        float roll = Random.value;
        if (roll < eliteChance) return EnemyVariant.Elite;
        if (roll < eliteChance + scoutChance) return EnemyVariant.Scout;
        return EnemyVariant.Normal;
    }

    // ── Spawn position ────────────────────────────────────────────────────────

    /// <summary>
    /// Geeft een spawn-positie 30 m buiten de circulaire map-rand,
    /// globaal in de opgegeven richting (met een kleine willekeurige spreiding).
    /// </summary>
    private Vector3 GetSpawnPositionCircular(Vector3 direction)
    {
        float radius = MapController.Instance.CurrentRadius + spawnBeyondEdge;

        Vector3 dir;
        if (direction == Vector3.zero)
        {
            float rndAngle = Random.Range(0f, 360f);
            dir = Quaternion.Euler(0f, rndAngle, 0f) * Vector3.forward;
        }
        else
        {
            dir = new Vector3(direction.x, 0f, direction.z).normalized;
            float spread = Random.Range(-spawnAngleSpread, spawnAngleSpread);
            dir = Quaternion.Euler(0f, spread, 0f) * dir;
        }

        Vector3 pos = transform.position + dir * radius;
        pos.y = MapController.Instance.SurfaceY + spawnYOffset;
        return pos;
    }

    private Vector3 GetSpawnPositionInDirection(Vector3 direction)
    {
        if (edgePoints.Count == 0)
            return Vector3.zero;

        if (direction == Vector3.zero)
            return edgePoints[Random.Range(0, edgePoints.Count)];

        Vector3 dirFlat = new Vector3(direction.x, 0f, direction.z).normalized;
        Vector3 center  = transform.position;

        const float spreadAngle = 40f;
        var candidates = new List<Vector3>();
        foreach (Vector3 point in edgePoints)
        {
            Vector3 toPoint = point - center;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude < 0.01f) continue;
            if (Vector3.Angle(dirFlat, toPoint.normalized) <= spreadAngle)
                candidates.Add(point);
        }

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        Vector3 best  = edgePoints[0];
        float bestDot = -2f;
        foreach (Vector3 point in edgePoints)
        {
            Vector3 toPoint = point - center;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude < 0.01f) continue;
            float dot = Vector3.Dot(dirFlat, toPoint.normalized);
            if (dot > bestDot) { bestDot = dot; best = point; }
        }
        return best;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void CleanupAllEnemies()
    {
        foreach (var enemy in activeEnemies)
            if (enemy != null) Destroy(enemy);
        activeEnemies.Clear();
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }

    // ── Edge baking ───────────────────────────────────────────────────────────

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
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (Vector3 p in edgePoints)
            Gizmos.DrawSphere(p, 0.5f);
    }
}
