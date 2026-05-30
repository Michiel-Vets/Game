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
    [Tooltip("Minimaal aantal pickups (battery + health samen) dat altijd op de map aanwezig moet zijn.")]
    [SerializeField] private int minTotalPickups = 2;
    [Tooltip("Hoe snel nieuwe pickups spawnen als het minimum niet gehaald wordt (seconden).")]
    [SerializeField] private float emergencySpawnInterval = 4f;

    [Header("Spawn Interval")]
    [SerializeField] private float batterySpawnInterval = 15f;
    [SerializeField] private float healthSpawnInterval = 20f;

    [Header("Spawn Zone")]
    [SerializeField] private float spawnRadius = 20f;
    [SerializeField] private float minDistanceFromPlayer = 5f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 1f;

    private readonly List<GameObject> activeBatteryPickups = new List<GameObject>();
    private readonly List<GameObject> activeHealthPickups = new List<GameObject>();

    private Transform player;
    private float batteryTimer;
    private float healthTimer;
    private float emergencyTimer;
    private bool difficultyApplied;

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);
    }

    private void ApplyDifficulty()
    {
        DifficultySettings.Load();
        maxBatteryPickups = Mathf.Max(1, Mathf.RoundToInt(maxBatteryPickups * DifficultySettings.PickupMaxMultiplier));
        maxHealthPickups = Mathf.Max(1, Mathf.RoundToInt(maxHealthPickups * DifficultySettings.PickupMaxMultiplier));
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

        // Zorg dat er altijd minstens minTotalPickups op de kaart liggen
        int total = activeBatteryPickups.Count + activeHealthPickups.Count;
        if (total < minTotalPickups)
        {
            emergencyTimer -= Time.deltaTime;
            if (emergencyTimer <= 0f)
            {
                emergencyTimer = emergencySpawnInterval;
                // Spawn het type waarvan er minder is, of battery als gelijk
                if (activeBatteryPickups.Count <= activeHealthPickups.Count)
                    TrySpawn(batteryPickupPrefab, activeBatteryPickups);
                else
                    TrySpawn(healthPickupPrefab, activeHealthPickups);
            }
        }
        else
        {
            emergencyTimer = 0f;
        }
    }

    private void TrySpawn(GameObject prefab, List<GameObject> list)
    {
        if (prefab == null) return;

        float outerRadius, innerRadius;

        if (MapController.Instance != null)
        {
            float inset = MapController.Instance.HardWallInset;
            outerRadius = Mathf.Max(0f, MapController.Instance.GrowthRingOuterRadius - inset);
            innerRadius = Mathf.Max(0f, MapController.Instance.GrowthRingInnerRadius - inset);

            // Geen of te kleine groei → spawn overal op de kaart
            if (outerRadius - innerRadius < 2f)
                innerRadius = 0f;
        }
        else
        {
            outerRadius = spawnRadius;
            innerRadius = 0f;
        }

        for (int attempt = 0; attempt < 10; attempt++)
        {
            // Uniforme steekproef in een ring
            float minR2  = innerRadius * innerRadius;
            float maxR2  = outerRadius * outerRadius;
            float r      = Mathf.Sqrt(Random.Range(minR2, maxR2 + 0.001f));
            float angle  = Random.Range(0f, Mathf.PI * 2f);
            Vector2 random2D = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            Vector3 candidate = transform.position + new Vector3(random2D.x, 0f, random2D.y);

            if (Vector3.Distance(candidate, player.position) < minDistanceFromPlayer)
                continue;

            Vector3 rayOrigin = candidate + Vector3.up * raycastHeight;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                raycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
                continue;

            Vector3 spawnPos = hit.point + Vector3.up * spawnYOffset;
            GameObject pickup = Instantiate(prefab, spawnPos, Quaternion.identity);
            list.Add(pickup);
            return;
        }
    }

    /// <summary>Verwijdert alle huidige pickups; de normale timer spawnt ze na korte vertraging opnieuw.</summary>
    public void Reshuffle()
    {
        foreach (var go in activeBatteryPickups)
            if (go != null) Destroy(go);
        activeBatteryPickups.Clear();

        foreach (var go in activeHealthPickups)
            if (go != null) Destroy(go);
        activeHealthPickups.Clear();

        // Korte vertraging zodat de map volledig geschaald is vóór nieuwe pickups spawnen
        batteryTimer = 1.5f;
        healthTimer  = 2f;
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
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.color = Color.yellow;
        if (player != null)
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
    }
}