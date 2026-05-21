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
                              WaveType waveType, float healthMult, float speedMult, int spawnCap)
    {
        currentWaveNumber = wave;
        currentAggressionLevel = aggression;
        currentMaxEnemies = maxEnemies;
        currentSpawnInterval = spawnInterval;
        currentWaveType = waveType;
        currentEnemyHealthMultiplier = healthMult;
        currentEnemySpeedMultiplier = speedMult;
        currentSpawnCap = spawnCap;
        isActive = true;
        isBreak = false;
        spawnTimer = 0f;

        // Cleanup bestaande vijanden bij wave start (behalve eventuele scouts)
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
            controller.SetWaveData(currentWaveNumber, 0.1f); // Lage agressie
            controller.SetScoutMode(); // Scout mode: sneller, minder HP
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

            Vector3 spawnPos = edgePoints[Random.Range(0, edgePoints.Count)];
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.SetWaveData(currentWaveNumber, currentAggressionLevel);

                // Pas stats aan op basis van wave type
                if (currentWaveType == WaveType.Horde)
                    controller.SetHordeMode();
                else if (currentWaveType == WaveType.Elite)
                    controller.SetEliteMode();

                // Pas multipliers toe van difficulty/wave
                controller.ApplyMultipliers(currentEnemyHealthMultiplier, currentEnemySpeedMultiplier);

                ApplyVariant(controller);
            }

            activeEnemies.Add(enemy);
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