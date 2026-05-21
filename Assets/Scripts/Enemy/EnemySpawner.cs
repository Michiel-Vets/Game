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

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 1f;

    [Header("Elite Enemies (Normal Waves)")]
    [SerializeField] private float eliteChanceBase = 0.05f;
    [SerializeField] private float eliteChancePerWave = 0.02f;
    [SerializeField] private float eliteChanceMax = 0.30f;

    [Header("Scout Enemies (Normal Waves)")]
    [SerializeField] private float scoutChanceBase = 0.10f;
    [SerializeField] private float scoutChancePerWave = 0.015f;
    [SerializeField] private float scoutChanceMax = 0.35f;

    [Header("Scouts During Break")]
    [SerializeField] private GameObject scoutPrefab; // Optioneel: aparte prefab voor scouts

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<Vector3> edgePoints = new List<Vector3>();

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
        isActive = true;
        isBreak = false;
        spawnTimer = 0f;

        CleanupAllEnemies();
    }

    public void OnWaveBreak()
    {
        isActive = false;
        isBreak = true;
        // Verwijder niet alle enemies tijdens pauze - scouts blijven
    }

    public void SpawnScout()
    {
        if (!isBreak) return;
        if (activeEnemies.Count >= 3) return; // Max scouts tijdens pauze
        if (edgePoints.Count == 0) return;

        Vector3 spawnPos = edgePoints[Random.Range(0, edgePoints.Count)];
        GameObject enemy = Instantiate(scoutPrefab != null ? scoutPrefab : enemyPrefab, spawnPos, Quaternion.identity);

        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.SetWaveData(currentWaveNumber, 0.1f);
            controller.SetScoutMode();

            // Scout dropt een kleine batterij-pickup als hij verslagen wordt
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

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemies();
        }
    }

    private void SpawnEnemies()
    {
        if (activeEnemies.Count >= currentSpawnCap) return;
        if (edgePoints.Count == 0) return;

        int toSpawn = 1;
        if (currentWaveType == WaveType.Horde && currentWaveNumber > 3)
            toSpawn = Random.Range(1, 4); // Meerdere tegelijk spawnen

        for (int i = 0; i < toSpawn; i++)
        {
            if (activeEnemies.Count >= currentSpawnCap) break;

            Vector3 spawnPos = GetSpawnPositionInDirection(currentSpawnDirection);
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.SetWaveData(currentWaveNumber, currentAggressionLevel);

                if (currentWaveType == WaveType.Horde)
                    controller.SetHordeMode();
                else if (currentWaveType == WaveType.Elite)
                    controller.SetEliteMode();

                // Zichtbare enemies (direct in aanvalsmodus) krijgen iets minder HP
                // omdat ze al een voordeel hebben door meteen zichtbaar te zijn
                bool isVisible = controller.IsInAttackMode();
                controller.ApplyMultipliers(currentEnemyHealthMultiplier, currentEnemySpeedMultiplier, isVisible);

                ApplyVariant(controller);
            }

            activeEnemies.Add(enemy);
            WaveManager.Instance?.NotifyEnemySpawned();
        }
    }

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

        // Tijdens Horde waves meer scouts, tijdens Elite meer elites
        if (currentWaveType == WaveType.Horde)
            scoutChance *= 1.5f;
        else if (currentWaveType == WaveType.Elite)
            eliteChance *= 2f;

        float roll = Random.value;
        if (roll < eliteChance) return EnemyVariant.Elite;
        if (roll < eliteChance + scoutChance) return EnemyVariant.Scout;
        return EnemyVariant.Normal;
    }

    // Kiest een spawn punt binnen een kegel rondom de opgegeven wave-richting.
    // Dit geeft geesten een gezamenlijke aanvalsrichting terwijl ze iets gespreid blijven.
    private Vector3 GetSpawnPositionInDirection(Vector3 direction)
    {
        if (edgePoints.Count == 0)
            return Vector3.zero;

        if (direction == Vector3.zero)
            return edgePoints[Random.Range(0, edgePoints.Count)];

        Vector3 dirFlat = new Vector3(direction.x, 0f, direction.z).normalized;
        Vector3 center  = transform.position;

        // Verzamel alle edge points binnen een kegel van 40° rond de richting
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

        // Fallback: dichtste edge point in de richting
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