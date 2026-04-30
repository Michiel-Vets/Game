using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;

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
        HandleInput();

        if (isOn && damageEnemies)
        {
            HandleBeam();
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isOn = !isOn;
            ApplyState();
        }
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

        RaycastHit hit;

        // Check of licht geblokkeerd wordt
        if (Physics.Raycast(origin, direction, out hit, maxDistance, obstacleLayers))
        {
            maxDistance = hit.distance;
        }

        // Zoek enemies in de straal
        Collider[] hits = Physics.OverlapSphere(origin + direction * (maxDistance / 2f), hitRadius, enemyLayers);

        foreach (Collider col in hits)
        {
            EnemyController enemy = col.GetComponent<EnemyController>();
            if (enemy == null) continue;

            // Check of enemy effectief in de lichtstraal zit
            Vector3 toEnemy = (enemy.transform.position - origin).normalized;
            float dot = Vector3.Dot(direction, toEnemy);

            if (dot > 0.8f) // redelijk recht in de lichtstraal
            {
                enemy.ReceiveFlashlightHit();
            }
        }
    }
}