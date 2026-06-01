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

    [Header("Power-Up Scouts")]
    [Tooltip("Prefab voor de power-up drager scout (leeg = zelfde als scoutPrefab).")]
    [SerializeField] private GameObject powerUpScoutPrefab;
    [Tooltip("Prefab van het power-up item dat de scout dropt als hij gedood wordt.")]
    [SerializeField] private GameObject powerUpItemPrefab;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<GameObject> _breakEnemies = new List<GameObject>();
    private readonly List<Vector3> edgePoints = new List<Vector3>();

    private struct SpawnGroup
    {
        public Vector3 Direction;
        public int     Count;
        public float   TriggerTime; // Time.time waarop deze groep spawnt
    }
    private readonly List<SpawnGroup> _pendingGroups = new List<SpawnGroup>();

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
                              List<Vector3> spawnDirections, float groupDelay)
    {
        currentWaveNumber = wave;
        currentAggressionLevel = aggression;
        currentMaxEnemies = maxEnemies;
        currentSpawnInterval = spawnInterval;
        currentWaveType = waveType;
        currentEnemyHealthMultiplier = healthMult;
        currentEnemySpeedMultiplier = speedMult;
        currentSpawnCap = spawnCap;
        currentSpawnDirection = spawnDirections != null && spawnDirections.Count > 0
            ? spawnDirections[0] : Vector3.forward;
        isBreak = false;
        spawnTimer = 0f;
        _pendingGroups.Clear();

        // Map meeschalen met enemy count
        UpdateMapRadius(maxEnemies);

        // Break-scouts wegsturen zonder drop te triggeren
        foreach (var e in _breakEnemies)
        {
            if (e == null) continue;
            e.GetComponent<PowerUpDropReward>()?.CancelDrop();
            e.GetComponent<ScoutDropReward>()?.CancelDrop();
            var ec = e.GetComponent<EnemyController>();
            if (ec != null) ec.BeginFlyOut();
            else Destroy(e);
        }
        _breakEnemies.Clear();

        if (currentWaveType == WaveType.Siege)
        {
            isActive = true;
        }
        else
        {
            // Verdeel enemies gelijkmatig over de richtingen en plan ze met vertraging
            ScheduleSpawnGroups(spawnDirections ?? new List<Vector3> { currentSpawnDirection },
                                currentSpawnCap, groupDelay);
            isActive = false;
        }
    }

    /// <summary>
    /// Verdeelt <paramref name="totalCount"/> enemies over de opgegeven richtingen
    /// en scheduleert elke groep met een vertraging van <paramref name="groupDelay"/> seconden.
    /// </summary>
    private void ScheduleSpawnGroups(List<Vector3> directions, int totalCount, float groupDelay)
    {
        if (directions.Count == 0) return;

        int groupCount = directions.Count;
        int baseCount  = totalCount / groupCount;
        int remainder  = totalCount % groupCount;

        for (int i = 0; i < groupCount; i++)
        {
            int count = baseCount + (i < remainder ? 1 : 0);
            if (count <= 0) continue;
            _pendingGroups.Add(new SpawnGroup
            {
                Direction   = directions[i],
                Count       = count,
                TriggerTime = Time.time + i * groupDelay,
            });
        }
    }

    public void OnWaveBreak()
    {
        isActive = false;
        isBreak = true;
        _pendingGroups.Clear();
        CleanupAllEnemies();
    }

    public void SpawnScout()
    {
        if (!isBreak) return;
        int scoutCap = 3 + Mathf.FloorToInt(LastWaveSurvivorCount / 3f);
        if (_breakEnemies.Count >= scoutCap) return;

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

        _breakEnemies.Add(enemy);
    }

    /// <summary>Spawnt een scout binnen de map die bij dood een power-up dropt.</summary>
    public void SpawnPowerUpScout(GameObject dropPrefab = null)
    {
        Vector3 spawnPos = FindSpawnInsideMap();
        if (spawnPos == Vector3.zero) return;

        GameObject prefab = powerUpScoutPrefab != null ? powerUpScoutPrefab
                          : scoutPrefab        != null ? scoutPrefab
                          : enemyPrefab;
        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.SetWaveData(currentWaveNumber, 0f);
            controller.SetPowerUpCarrierMode();
            controller.SetSpawnedInsideMap();
        }

        // dropPrefab: eerst expliciete parameter, dan inspector-veld, dan Resources fallback
        GameObject resolvedDrop = dropPrefab != null ? dropPrefab : powerUpItemPrefab;
        PowerUpDropReward reward = enemy.GetComponent<PowerUpDropReward>()
                                ?? enemy.AddComponent<PowerUpDropReward>();
        reward.Setup(resolvedDrop);

        _breakEnemies.Add(enemy);
    }

    private Vector3 FindSpawnInsideMap()
    {
        if (MapController.Instance == null)
        {
            if (edgePoints.Count == 0) return Vector3.zero;
            return edgePoints[Random.Range(0, edgePoints.Count)];
        }

        float mapRadius = MapController.Instance.CurrentRadius
                        - MapController.Instance.HardWallInset - 3f;
        mapRadius = Mathf.Max(mapRadius, 5f);
        Vector3 center = MapController.Instance.PlatformCenter;

        for (int attempt = 0; attempt < 15; attempt++)
        {
            float r = Mathf.Sqrt(Random.Range(0.09f, 0.81f)) * mapRadius;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            Vector3 rayOrigin = candidate + Vector3.up * raycastHeight;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                    raycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
                continue;

            return hit.point + Vector3.up * spawnYOffset;
        }

        return Vector3.zero;
    }

    private void Update()
    {
        PlayerFinder.TryAssignIfNull(ref player);
        if (enemyPrefab == null || player == null) return;

        CleanupDestroyedEnemies();

        // Siege: timer-based spawning
        if (isActive && currentWaveType == WaveType.Siege)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= currentSpawnInterval)
            {
                spawnTimer = 0f;
                if (activeEnemies.Count < currentSpawnCap)
                    SpawnSingleEnemy(currentSpawnDirection);
            }
        }

        // Multi-directie groepen: spawnen zodra hun TriggerTime bereikt is
        for (int i = _pendingGroups.Count - 1; i >= 0; i--)
        {
            if (Time.time < _pendingGroups[i].TriggerTime) continue;
            ExecuteSpawnGroup(_pendingGroups[i].Direction, _pendingGroups[i].Count);
            _pendingGroups.RemoveAt(i);
        }
    }

    // ── Groepsspawn ───────────────────────────────────────────────────────────

    private void ExecuteSpawnGroup(Vector3 direction, int count)
    {
        for (int i = 0; i < count; i++)
            SpawnSingleEnemy(direction);
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
            controller.EnforceMinimumScale(); // harde cap: nooit kleiner dan 80 % van basisschaal
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
        LastWaveSurvivorCount = 0;
        foreach (var enemy in activeEnemies)
        {
            if (enemy == null) continue;
            var ec = enemy.GetComponent<EnemyController>();
            if (ec != null)
            {
                LastWaveSurvivorCount++;
                ec.BeginFlyOut();
            }
            else
            {
                Destroy(enemy);
            }
        }
        activeEnemies.Clear();
    }

    public int LastWaveSurvivorCount { get; private set; }

    private void CleanupDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
            if (activeEnemies[i] == null) activeEnemies.RemoveAt(i);
        for (int i = _breakEnemies.Count - 1; i >= 0; i--)
            if (_breakEnemies[i] == null) _breakEnemies.RemoveAt(i);
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
