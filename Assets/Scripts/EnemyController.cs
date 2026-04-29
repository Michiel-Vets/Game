using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool rotateTowardsPlayer = true;

    [Header("Combat")]
    [SerializeField, Range(0f, 1f)] private float damagePercentage = 0.1f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float upwardKnockbackForce = 2f;

    private Transform playerTarget;
    private Rigidbody rb;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                playerTarget = player.transform;
            else
                Debug.LogWarning("EnemyController: No player found with tag 'Player'.");
        }
    }

    private void FixedUpdate()
    {
        MoveTowardsPlayer();
    }

    public void SetTarget(Transform target)
    {
        playerTarget = target;
    }

    private void MoveTowardsPlayer()
    {
        if (playerTarget == null || hasHit)
            return;

        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Vector3 horizontalVelocity = direction.normalized * moveSpeed;

        rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            rb.linearVelocity.y,
            horizontalVelocity.z
        );

        if (rotateTowardsPlayer)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            rb.MoveRotation(targetRotation);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit)
            return;

        HealthController health = collision.gameObject.GetComponentInParent<HealthController>();

        if (health == null)
            return;

        if (!health.CompareTag("Player"))
            return;

        hasHit = true;

        float damageAmount = health.GetMaxHealth() * damagePercentage;
        health.TakeDamage(damageAmount);

        PlayerController playerController = health.GetComponent<PlayerController>();

        if (playerController != null)
        {
            Vector3 knockbackDirection = health.transform.position - transform.position;
            knockbackDirection.y = 0f;

            playerController.ApplyKnockback(
                knockbackDirection,
                knockbackForce,
                upwardKnockbackForce
            );
        }

        Destroy(gameObject);
    }
}