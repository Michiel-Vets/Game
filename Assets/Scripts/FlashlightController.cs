using UnityEngine;

public class FlashlightController : MonoBehaviour
{
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
    [Tooltip("Afstand waarop de flashlight op volle kracht werkt (damage & slowdown).")]
    [SerializeField] private float fullEffectDistance = 5f;
    [Tooltip("Minimale effectiviteit op maximale afstand (0 = geen effect, 1 = geen falloff).")]
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
        if (isOn && damageEnemies)
            HandleBeam();
    }

    public void Toggle()
    {
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

        RaycastHit[] hits = Physics.SphereCastAll(
            origin, hitRadius, direction, effectiveDistance, enemyLayers);

        foreach (RaycastHit hit in hits)
        {
            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            // Bereken hoe ver de enemy van de flashlight-oorsprong verwijderd is
            // en schaal schade + vertraging lineair af op afstand.
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