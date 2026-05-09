using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BatteryController batteryController;

    [Header("Light")]
    [SerializeField] private Light flashlight;
    [SerializeField] private bool startsOn = true;

    [Header("Beam Settings")]
    [SerializeField] private float maxDistance = 25f;
    [SerializeField] private float hitRadius = 1.2f;

    [Header("Damage / Weaken")]
    [SerializeField] private bool damageEnemies = true;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Distance Falloff")]
    [SerializeField] private float fullEffectDistance = 5f;
    [SerializeField, Range(0f, 1f)] private float minEffectAtMaxDistance = 0.1f;

    public bool IsOn => isOn;

    private bool isOn;

    void Awake()
    {
        if (flashlight == null)
            flashlight = GetComponent<Light>();
    }

    void Start()
    {
        isOn = startsOn;
        ApplyState();
    }

    void Update()
    {
        if (isOn)
        {
            if (batteryController != null)
            {
                batteryController.DrainBattery(Time.deltaTime);

                if (!batteryController.HasBattery)
                {
                    isOn = false;
                    ApplyState();
                    return;
                }
            }

            if (damageEnemies)
                HandleBeam();
        }
    }

    public void Toggle()
    {
        if (!isOn && batteryController != null && !batteryController.HasBattery)
            return;

        isOn = !isOn;
        ApplyState();
    }

    private void ApplyState()
    {
        if (flashlight != null)
            flashlight.enabled = isOn;
    }

    private void HandleBeam()
    {
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        float effectiveDistance = maxDistance;
        if (obstacleLayers != 0 &&
            Physics.Raycast(origin, direction, out RaycastHit obstacleHit, maxDistance, obstacleLayers))
        {
            effectiveDistance = obstacleHit.distance;
        }

        RaycastHit[] hits = Physics.SphereCastAll(origin, hitRadius, direction, effectiveDistance, enemyLayers);

        foreach (RaycastHit hit in hits)
        {
            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            float hitDistance = Vector3.Distance(origin, hit.point);
            float distanceFraction = Mathf.Clamp01(hitDistance / maxDistance);
            float effectFactor = Mathf.Lerp(1f, minEffectAtMaxDistance,
                Mathf.InverseLerp(fullEffectDistance / maxDistance, 1f, distanceFraction));

            enemy.ReceiveFlashlightHit(effectFactor);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
    }
}