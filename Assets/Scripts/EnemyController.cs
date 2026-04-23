using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private HealthController playerHealthController;
    [SerializeField] private Collider enemyCollider;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool rotateTowardsPlayer = true;

    [Header("Height")]
    [SerializeField] private bool lockHeightToStartPosition = true;
    [SerializeField] private float extraHeightOffset = 0f;

    [Header("Damage")]
    [SerializeField] private float damageAmount = 20f;

    private Rigidbody rb;
    private bool hasHitPlayer;
    private float lockedYPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (enemyCollider == null)
        {
            enemyCollider = GetComponent<Collider>();
        }

        lockedYPosition = transform.position.y + extraHeightOffset;
    }

    private void Start()
    {
        FindPlayerTarget();
        FindPlayerHealthController();
        SnapToLockedHeight();
    }

    private void FixedUpdate()
    {
        if (hasHitPlayer)
            return;

        MoveTowardsPlayer();
    }

    private void FindPlayerTarget()
    {
        if (playerTarget != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            playerTarget = playerObject.transform;
        }
    }

    private void FindPlayerHealthController()
    {
        if (playerHealthController != null)
            return;

        if (playerTarget == null)
            FindPlayerTarget();

        if (playerTarget == null)
            return;

        playerHealthController = playerTarget.GetComponent<HealthController>();

        if (playerHealthController == null)
            playerHealthController = playerTarget.GetComponentInChildren<HealthController>(true);

        if (playerHealthController == null)
            playerHealthController = playerTarget.GetComponentInParent<HealthController>();
    }

    private void SnapToLockedHeight()
    {
        if (!lockHeightToStartPosition)
            return;

        Vector3 correctedPosition = rb.position;
        correctedPosition.y = lockedYPosition;
        rb.position = correctedPosition;
    }

    private void MoveTowardsPlayer()
    {
        if (playerTarget == null)
            return;

        Vector3 directionToPlayer = playerTarget.position - rb.position;
        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude <= 0.001f)
            return;

        Vector3 moveDirection = directionToPlayer.normalized;
        Vector3 targetPosition = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;

        if (lockHeightToStartPosition)
        {
            targetPosition.y = lockedYPosition;
        }

        rb.MovePosition(targetPosition);

        if (rotateTowardsPlayer)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.MoveRotation(targetRotation);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHitPlayer)
            return;

        Transform hitRoot = collision.transform.root;

        if (!hitRoot.CompareTag("Player"))
            return;

        hasHitPlayer = true;

        HealthController hitHealthController = hitRoot.GetComponent<HealthController>();

        if (hitHealthController == null)
            hitHealthController = hitRoot.GetComponentInChildren<HealthController>(true);

        if (hitHealthController == null)
            hitHealthController = hitRoot.GetComponentInParent<HealthController>();

        if (hitHealthController == null && playerHealthController != null)
            hitHealthController = playerHealthController;

        if (hitHealthController != null && hitHealthController.IsAlive)
        {
            hitHealthController.TakeDamage(damageAmount);
            Debug.Log("Enemy deed schade aan de speler: " + damageAmount);
        }
        else
        {
            Debug.LogWarning("EnemyController: geen HealthController gevonden op de Player.");
        }

        Destroy(gameObject);
    }
}