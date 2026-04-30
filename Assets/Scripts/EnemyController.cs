using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    private enum LungeState { None, Preparing, Lunging, Exhausted }
    private enum GhostRole { DirectAttacker, Flanker, HighGhost }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Ghost Flying")]
    [SerializeField] private LayerMask floorAndObstacleLayers;
    [SerializeField] private float maxHeightAboveSurface = 5f;
    [SerializeField] private float preferredFloatHeight = 1.6f;
    [SerializeField] private float verticalSpeed = 3.5f;
    [SerializeField] private float heightRaycastDistance = 30f;
    [SerializeField] private float heightFollowDistance = 12f;

    [Header("Random Flying Behaviour")]
    [SerializeField, Range(0f, 1f)] private float chanceToFlyUp = 0.35f;
    [SerializeField] private float minReturnToGroundDistance = 2.5f;
    [SerializeField] private float maxReturnToGroundDistance = 7f;
    [SerializeField] private float minExtraFlyHeight = 1f;
    [SerializeField] private float maxExtraFlyHeight = 4f;

    [Header("Surround Player")]
    [SerializeField, Range(0f, 1f)] private float flankChance = 0.45f;
    [SerializeField] private float flankDistance = 4f;
    [SerializeField] private float flankSwitchInterval = 4f;
    [SerializeField] private float surroundStrength = 1.2f;

    [Header("Climb Over Enemies")]
    [SerializeField] private float climbOverEnemyChance = 0.35f;
    [SerializeField] private float climbOverEnemyHeight = 2.2f;
    [SerializeField] private float climbCheckDistance = 3f;

    [Header("Lunge Attack")]
    [SerializeField] private float lungeTriggerDistance = 5f;
    [SerializeField, Range(0f, 1f)] private float lungeChance = 0.1f;
    [SerializeField] private float lungePrepareTime = 2f;
    [SerializeField] private float lungeSpeed = 22f;
    [SerializeField] private float lungeDuration = 0.45f;
    [SerializeField] private float lungeExhaustTime = 5f;
    [SerializeField] private float lungeCooldown = 45f;
    [SerializeField] private float lungeDecisionInterval = 1f;

    [Header("Aggression")]
    [SerializeField] private float attackDistance = 2.2f;
    [SerializeField] private float closeRangeAggressionMultiplier = 1.35f;
    [SerializeField] private float playerFocusStrength = 2.5f;

    [Header("Obstacle / Enemy Avoidance")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float obstacleCheckDistance = 3f;
    [SerializeField] private float obstacleCheckRadius = 0.45f;
    [SerializeField] private float enemyAvoidRadius = 1.8f;
    [SerializeField] private float enemyAvoidStrength = 0.8f;

    [Header("Distance Speed Scaling")]
    [SerializeField] private float normalSpeedDistance = 15f;
    [SerializeField] private float maxSpeedDistance = 50f;
    [SerializeField] private float maxDistanceSpeedMultiplier = 2f;

    [Header("Enemy Sprint")]
    [SerializeField] private float sprintSpeedMultiplier = 1.5f;
    [SerializeField] private float rechargeSpeedMultiplier = 0.65f;
    [SerializeField] private float minSprintDuration = 1f;
    [SerializeField] private float maxSprintDuration = 5f;
    [SerializeField] private float rechargeMultiplier = 1.5f;

    [Header("Combat")]
    [SerializeField, Range(0f, 1f)] private float damagePercentage = 0.1f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float upwardKnockbackForce = 2f;

    private Transform playerTarget;
    private Rigidbody rb;

    private bool hasHit;
    private bool isSprinting;
    private bool isRecharging;
    private bool wantsToFlyUp;
    private bool likesClimbingEnemies;

    private float personalExtraFlyHeight;
    private float returnToGroundDistance;

    private GhostRole ghostRole;
    private float flankSide;
    private float flankTimer;

    // Sprint timers
    private float sprintTimer;
    private float rechargeTimer;
    private float chosenSprintDuration;

    // Lunge — timers vervangen door Cooldown waar van toepassing
    private LungeState lungeState = LungeState.None;
    private float lungeTimer;
    private readonly Cooldown lungeCooldownTimer = new Cooldown();
    private readonly Cooldown lungeDecisionTimer = new Cooldown();
    private Vector3 lungeDirection;

    private Vector3 currentMoveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        wantsToFlyUp = Random.value <= chanceToFlyUp;
        likesClimbingEnemies = Random.value <= climbOverEnemyChance;
        personalExtraFlyHeight = Random.Range(minExtraFlyHeight, maxExtraFlyHeight);
        returnToGroundDistance = Random.Range(minReturnToGroundDistance, maxReturnToGroundDistance);

        PickGhostRole();

        lungeCooldownTimer.ResetRandom(0f, 8f);
        lungeDecisionTimer.ResetRandom(0f, lungeDecisionInterval);

        StartNewSprint();
    }

    private void Start()
    {
        if (playerTarget == null && !PlayerFinder.TryAssignIfNull(ref playerTarget))
            Debug.LogWarning("EnemyController: No player found with tag 'Player'.");
    }

    private void FixedUpdate()
    {
        if (playerTarget == null || hasHit)
            return;

        float dt = Time.fixedDeltaTime;

        lungeCooldownTimer.Tick(dt);
        lungeDecisionTimer.Tick(dt);

        if (UpdateLungeState())
            return;

        UpdateSprintState();
        UpdateFlankBehaviour();
        TryStartLunge();
        MoveGhostAggressively();
    }

    public void SetTarget(Transform target)
    {
        playerTarget = target;
    }

    // ── Ghost Role ───────────────────────────────────────────────────────────

    private void PickGhostRole()
    {
        if (Random.value <= flankChance)
            ghostRole = GhostRole.Flanker;
        else if (wantsToFlyUp)
            ghostRole = GhostRole.HighGhost;
        else
            ghostRole = GhostRole.DirectAttacker;

        flankSide = Random.value < 0.5f ? -1f : 1f;
        flankTimer = Random.Range(0f, flankSwitchInterval);
    }

    private void UpdateFlankBehaviour()
    {
        if (ghostRole != GhostRole.Flanker)
            return;

        flankTimer -= Time.fixedDeltaTime;

        if (flankTimer <= 0f)
        {
            flankTimer = Random.Range(flankSwitchInterval * 0.7f, flankSwitchInterval * 1.3f);

            if (Random.value < 0.35f)
                flankSide *= -1f;
        }
    }

    // ── Movement ─────────────────────────────────────────────────────────────

    private void MoveGhostAggressively()
    {
        Vector3 toPlayer = playerTarget.position - transform.position;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer < 0.01f)
            return;

        Vector3 directionToPlayerFlat = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;

        RotateTowardsPlayer(directionToPlayerFlat);

        Vector3 attackDirection = GetAttackDirection(directionToPlayerFlat, distanceToPlayer);
        Vector3 enemyAvoidance = GetEnemyAvoidance();

        Vector3 finalHorizontalDirection =
            attackDirection * playerFocusStrength +
            enemyAvoidance * enemyAvoidStrength;

        if (IsObstacleInFront(directionToPlayerFlat))
            finalHorizontalDirection += directionToPlayerFlat * 1.5f;

        finalHorizontalDirection.y = 0f;

        if (finalHorizontalDirection.sqrMagnitude < 0.01f)
            finalHorizontalDirection = directionToPlayerFlat;

        currentMoveDirection = Vector3.Lerp(
            currentMoveDirection,
            finalHorizontalDirection.normalized,
            10f * Time.fixedDeltaTime
        ).normalized;

        float speed = GetTargetSpeed(distanceToPlayer);

        if (distanceToPlayer <= attackDistance)
            speed *= closeRangeAggressionMultiplier;

        Vector3 horizontalVelocity = currentMoveDirection * speed;
        float targetY = GetTargetFlyingHeight(distanceToPlayer, directionToPlayerFlat);
        float verticalVelocity = GetSmoothVerticalVelocity(targetY);

        rb.linearVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z),
            acceleration * Time.fixedDeltaTime
        );
    }

    private Vector3 GetAttackDirection(Vector3 directionToPlayerFlat, float distanceToPlayer)
    {
        if (ghostRole != GhostRole.Flanker || distanceToPlayer <= attackDistance + 0.7f)
            return directionToPlayerFlat;

        Vector3 sideDirection = Vector3.Cross(Vector3.up, directionToPlayerFlat).normalized * flankSide;

        Vector3 desiredFlankPoint =
            playerTarget.position -
            directionToPlayerFlat * flankDistance +
            sideDirection * flankDistance;

        Vector3 toFlankPoint = desiredFlankPoint - transform.position;
        toFlankPoint.y = 0f;

        if (toFlankPoint.sqrMagnitude < 0.01f)
            return directionToPlayerFlat;

        return Vector3.Lerp(directionToPlayerFlat, toFlankPoint.normalized, surroundStrength).normalized;
    }

    // ── Flying Height ─────────────────────────────────────────────────────────

    private float GetTargetFlyingHeight(float distanceToPlayer, Vector3 directionToPlayerFlat)
    {
        float surfaceY = GetSurfaceHeightBelow();
        float restHeight = surfaceY + preferredFloatHeight;
        float maxAllowedHeight = surfaceY + maxHeightAboveSurface;

        float desiredHeight = restHeight;

        if (distanceToPlayer > returnToGroundDistance)
        {
            if (wantsToFlyUp)
                desiredHeight = restHeight + personalExtraFlyHeight;

            if (distanceToPlayer <= heightFollowDistance)
                desiredHeight = Mathf.Max(desiredHeight, playerTarget.position.y);

            if (likesClimbingEnemies)
            {
                float climbHeight = GetEnemyClimbHeight(directionToPlayerFlat);
                if (climbHeight > desiredHeight)
                    desiredHeight = climbHeight;
            }
        }

        return Mathf.Clamp(desiredHeight, restHeight, maxAllowedHeight);
    }

    private float GetEnemyClimbHeight(Vector3 directionToPlayerFlat)
    {
        Vector3 checkCenter =
            transform.position +
            directionToPlayerFlat * climbCheckDistance +
            Vector3.up * 0.8f;

        Collider[] nearbyEnemies = Physics.OverlapSphere(
            checkCenter, enemyAvoidRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        float highestEnemyY = float.MinValue;

        foreach (Collider enemy in nearbyEnemies)
        {
            if (enemy.transform == transform || enemy.attachedRigidbody == rb)
                continue;

            highestEnemyY = Mathf.Max(highestEnemyY, enemy.bounds.max.y);
        }

        return highestEnemyY == float.MinValue ? float.MinValue : highestEnemyY + climbOverEnemyHeight;
    }

    // ── Lunge ─────────────────────────────────────────────────────────────────

    private void TryStartLunge()
    {
        if (!lungeCooldownTimer.IsReady || !lungeDecisionTimer.IsReady)
            return;

        lungeDecisionTimer.Reset(lungeDecisionInterval);

        float distanceToPlayer = Vector3.Distance(playerTarget.position, transform.position);

        if (distanceToPlayer > lungeTriggerDistance || Random.value > lungeChance)
            return;

        lungeDirection = (playerTarget.position - transform.position).normalized;
        lungeState = LungeState.Preparing;
        lungeTimer = lungePrepareTime;
        lungeCooldownTimer.Reset(lungeCooldown);

        isSprinting = false;
        isRecharging = false;
        rb.linearVelocity = Vector3.zero;
    }

    private bool UpdateLungeState()
    {
        if (lungeState == LungeState.None)
            return false;

        lungeTimer -= Time.fixedDeltaTime;

        switch (lungeState)
        {
            case LungeState.Preparing:
                MoveVerticallyToRestHeight();
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

                Vector3 toPlayer = playerTarget.position - transform.position;
                if (toPlayer.sqrMagnitude > 0.01f)
                    lungeDirection = toPlayer.normalized;

                RotateTowardsPlayer(new Vector3(lungeDirection.x, 0f, lungeDirection.z));

                if (lungeTimer <= 0f)
                {
                    lungeState = LungeState.Lunging;
                    lungeTimer = lungeDuration;
                }
                return true;

            case LungeState.Lunging:
                rb.linearVelocity = lungeDirection * lungeSpeed;

                if (lungeTimer <= 0f)
                {
                    lungeState = LungeState.Exhausted;
                    lungeTimer = lungeExhaustTime;
                    rb.linearVelocity = Vector3.zero;
                }
                return true;

            case LungeState.Exhausted:
                MoveVerticallyToRestHeight();
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

                if (lungeTimer <= 0f)
                {
                    lungeState = LungeState.None;
                    StartNewSprint();
                }
                return true;
        }

        return false;
    }

    // ── Sprint ────────────────────────────────────────────────────────────────

    private void UpdateSprintState()
    {
        if (isSprinting)
        {
            sprintTimer -= Time.fixedDeltaTime;

            if (sprintTimer <= 0f)
            {
                isSprinting = false;
                isRecharging = true;
                rechargeTimer = chosenSprintDuration * rechargeMultiplier;
            }
            return;
        }

        if (isRecharging)
        {
            rechargeTimer -= Time.fixedDeltaTime;

            if (rechargeTimer <= 0f)
            {
                isRecharging = false;
                StartNewSprint();
            }
        }
    }

    private void StartNewSprint()
    {
        chosenSprintDuration = Random.Range(minSprintDuration, maxSprintDuration);
        sprintTimer = chosenSprintDuration;
        isSprinting = true;
        isRecharging = false;
    }

    // ── Speed ─────────────────────────────────────────────────────────────────

    private float GetTargetSpeed(float distanceToPlayer)
    {
        float stateMultiplier = isSprinting ? sprintSpeedMultiplier :
                                isRecharging ? rechargeSpeedMultiplier : 1f;

        return moveSpeed * GetDistanceSpeedMultiplier(distanceToPlayer) * stateMultiplier;
    }

    private float GetDistanceSpeedMultiplier(float distanceToPlayer)
    {
        if (distanceToPlayer <= normalSpeedDistance)
            return 1f;

        float t = Mathf.InverseLerp(normalSpeedDistance, maxSpeedDistance, distanceToPlayer);
        return Mathf.Lerp(1f, maxDistanceSpeedMultiplier, t);
    }

    // ── Physics Helpers ───────────────────────────────────────────────────────

    private void RotateTowardsPlayer(Vector3 directionToPlayer)
    {
        if (directionToPlayer.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private void MoveVerticallyToRestHeight()
    {
        float restHeight = GetSurfaceHeightBelow() + preferredFloatHeight;
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            GetSmoothVerticalVelocity(restHeight),
            rb.linearVelocity.z
        );
    }

    private float GetSmoothVerticalVelocity(float targetY)
    {
        return Mathf.Clamp((targetY - transform.position.y) * verticalSpeed, -verticalSpeed, verticalSpeed);
    }

    private float GetSurfaceHeightBelow()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 10f;

        return Physics.Raycast(
            rayOrigin, Vector3.down, out RaycastHit hit,
            heightRaycastDistance, floorAndObstacleLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : 0f;
    }

    private bool IsObstacleInFront(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * 0.7f;

        return Physics.SphereCast(
            origin, obstacleCheckRadius, direction, out _,
            obstacleCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetEnemyAvoidance()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(
            transform.position, enemyAvoidRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        Vector3 avoidance = Vector3.zero;

        foreach (Collider enemy in nearbyEnemies)
        {
            if (enemy.transform == transform || enemy.attachedRigidbody == rb)
                continue;

            Vector3 away = transform.position - enemy.transform.position;
            away.y = 0f;

            float distance = away.magnitude;

            if (distance < 0.01f)
                continue;

            avoidance += away.normalized / distance;
        }

        return avoidance;
    }

    // ── Combat ────────────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit)
            return;

        HealthController health = collision.gameObject.GetComponentInParent<HealthController>();

        if (health == null || !health.CompareTag("Player"))
            return;

        hasHit = true;

        health.TakeDamage(health.MaxHealth * damagePercentage);

        PlayerController playerController = health.GetComponent<PlayerController>();

        if (playerController != null)
        {
            Vector3 knockbackDirection = health.transform.position - transform.position;
            knockbackDirection.y = 0f;
            playerController.ApplyKnockback(knockbackDirection, knockbackForce, upwardKnockbackForce);
        }

        Destroy(gameObject);
    }
}