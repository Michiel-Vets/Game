using System.Collections.Generic;
using UnityEngine;

public class PickupSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject batteryPickupPrefab;
    [SerializeField] private GameObject healthPickupPrefab;

    [Header("Spawn Limieten")]
    [SerializeField] private int maxBatteryPickups = 3;
    [SerializeField] private int maxHealthPickups = 3;

    [Header("Spawn Interval")]
    [SerializeField] private float batterySpawnInterval = 15f;
    [SerializeField] private float healthSpawnInterval = 20f;

    [Header("Spawn Zone")]
    [SerializeField] private float spawnRadius = 120f;
    [SerializeField] private float minDistanceFromPlayer = 10f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 50f;
    [SerializeField] private float spawnYOffset = 1f;

    [Header("Vegetation Blocking")]
    [SerializeField] private LayerMask vegetationLayer;
    [SerializeField] private float vegetationCheckRadius = 2f;

    private readonly List<GameObject> activeBatteryPickups = new();
    private readonly List<GameObject> activeHealthPickups = new();

    private Transform player;
    private float batteryTimer;
    private float healthTimer;
    private bool difficultyApplied;

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);
    }

    private void ApplyDifficulty()
    {
        DifficultySettings.Load();

        maxBatteryPickups =
            Mathf.Max(1,
            Mathf.RoundToInt(maxBatteryPickups *
            DifficultySettings.PickupMaxMultiplier));

        maxHealthPickups =
            Mathf.Max(1,
            Mathf.RoundToInt(maxHealthPickups *
            DifficultySettings.PickupMaxMultiplier));

        batterySpawnInterval *= DifficultySettings.PickupIntervalMultiplier;
        healthSpawnInterval *= DifficultySettings.PickupIntervalMultiplier;

        batteryTimer = batterySpawnInterval;
        healthTimer = healthSpawnInterval;

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
        if (player == null) return;

        CleanupDestroyed(activeBatteryPickups);
        CleanupDestroyed(activeHealthPickups);

        batteryTimer -= Time.deltaTime;

        if (batteryTimer <= 0f)
        {
            batteryTimer = batterySpawnInterval;

            if (activeBatteryPickups.Count < maxBatteryPickups)
                TrySpawn(batteryPickupPrefab, activeBatteryPickups);
        }

        healthTimer -= Time.deltaTime;

        if (healthTimer <= 0f)
        {
            healthTimer = healthSpawnInterval;

            if (activeHealthPickups.Count < maxHealthPickups)
                TrySpawn(healthPickupPrefab, activeHealthPickups);
        }
    }

    private void TrySpawn(GameObject prefab, List<GameObject> list)
    {
        if (prefab == null) return;

        for (int attempt = 0; attempt < 25; attempt++)
        {
            Vector2 random2D = Random.insideUnitCircle * spawnRadius;

            Vector3 candidate =
                player.position +
                new Vector3(random2D.x, 0f, random2D.y);

            if (Vector3.Distance(candidate, player.position)
                < minDistanceFromPlayer)
                continue;

            Vector3 rayOrigin =
                candidate + Vector3.up * raycastHeight;

            if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                raycastHeight * 2f,
                groundLayer,
                QueryTriggerInteraction.Ignore))
                continue;

            Vector3 spawnPos =
                hit.point + Vector3.up * spawnYOffset;

            bool blocked =
                Physics.CheckSphere(
                    spawnPos,
                    vegetationCheckRadius,
                    vegetationLayer,
                    QueryTriggerInteraction.Ignore);

            if (blocked)
                continue;

            GameObject pickup =
                Instantiate(prefab, spawnPos, Quaternion.identity);

            list.Add(pickup);
            return;
        }
    }

    private void CleanupDestroyed(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null)
                list.RemoveAt(i);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        if (player != null)
            Gizmos.DrawWireSphere(player.position, spawnRadius);

        Gizmos.color = Color.yellow;

        if (player != null)
            Gizmos.DrawWireSphere(player.position,
                minDistanceFromPlayer);
    }
}