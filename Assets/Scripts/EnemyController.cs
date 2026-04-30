using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    private enum BehaviourState { Inactive, Wander, Chase, Flank, Intercept, Lunge, Recoil }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;
    [SerializeField] private float wanderSpeed = 1.8f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Flying")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float preferredFloatHeight = 1.6f;
    [SerializeField] private float maxFloatHeight = 5f;
    [SerializeField] private float verticalSpeed = 4f;
    [SerializeField] private float heightRaycastDistance = 30f;

    [Header("Random Flying Behaviour")]
    [SerializeField, Range(0f, 1f)] private float chanceToFlyHigh = 0.35f;
    [SerializeField] private float minExtraFlyHeight = 1f;
    [SerializeField] private float maxExtraFlyHeight = 4f;
    [SerializeField] private float heightFollowDistance = 12f;
    [SerializeField, Range(0f, 1f)] private float climbOverEnemyChance = 0.35f;
    [SerializeField] private float climbOverEnemyHeight = 2.2f;
    [SerializeField] private float climbCheckDistance = 3f;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 25f;
    [SerializeField] private float lostRadius = 35f;

    [Header("Distance Speed Boost")]
    [SerializeField] private float boostStartDistance = 15f;
    [SerializeField] private float boostMaxDistance = 50f;
    [SerializeField] private float maxBoostMultiplier = 2f;

    [Header("Surround & Flank")]
    [SerializeField, Range(0f, 1f)] private float flankChance = 0.45f;
    [SerializeField] private float flankDuration = 12f;
    [SerializeField] private float flankOrbitRadius = 5f;
    [SerializeField] private float flankOrbitSpeed = 1.2f;
    [SerializeField, Range(0f, 1f)] private float visibilityDotThreshold = 0.5f;

    [Header("Intercept")]
    [SerializeField] private float interceptDistance = 20f;
    [SerializeField] private float interceptLookAhead = 1.2f;

    [Header("Lunge")]
    [SerializeField] private float lungeTriggerDistance = 5f;
    [SerializeField, Range(0f, 1f)] private float lungeChance = 0.1f;
    [SerializeField] private float lungePrepareTime = 1.5f;
    [SerializeField] private float lungeSpeed = 22f;
    [SerializeField] private float lungeDuration = 0.45f;
    [SerializeField] private float lungeExhaustTime = 4f;
    [SerializeField] private float lungeCooldown = 40f;

    [Header("Separation")]
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float separationRadius = 2.5f;
    [SerializeField] private float separationStrength = 3f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float avoidanceDistance = 4f;
    [SerializeField] private float avoidanceRadius = 0.5f;
    [SerializeField] private float avoidanceStrength = 4f;

    [Header("Boundary")]
    [SerializeField] private float boundaryLookAhead = 3f;
    [SerializeField] private float boundaryStrength = 6f;

    [Header("Wander Flocking")]
    [SerializeField] private float flockRadius = 18f;
    [SerializeField] private float flockStrength = 2f;
    [SerializeField] private int flockMinGroupSize = 2;

    [Header("Fatigue")]
    [SerializeField] private float fatigueStartTime = 30f;
    [SerializeField] private float fatigueDuration = 10f;
    [SerializeField] private float fatigueMinMultiplier = 0.7f;

    [Header("Damage Reaction")]
    [SerializeField] private float recoilForce = 6f;
    [SerializeField] private float recoilDuration = 0.35f;

    [Header("Combat")]
    [SerializeField] private float attackDistance = 2.2f;
    [SerializeField, Range(0f, 1f)] private float damagePercentage = 0.1f;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float upwardKnockbackForce = 2f;

    private Rigidbody rb;
    private Transform playerTarget;
    private Transform playerCamera;

    private BehaviourState state = BehaviourState.Inactive;

    private bool wantsToFlyHigh;
    private bool likesClimbingEnemies;
    private float personalExtraFlyHeight;
    private float returnToGroundDistance;

    private float startDelay;
    private float aliveTime;
    private float stateTimer;
    private bool hasHit;

    private Vector3 smoothedVelocity;

    private Vector3 lastKnownPlayerPos;
    private bool hasLastKnownPos;

    private float flankAngle;
    private float flankSide;

    private float lungeTimer;
    private Vector3 lungeDirection;
    private Vector3 lungeMoveVelocity;
    private readonly Cooldown lungeCooldownTimer = new Cooldown();

    private float recoilTimer;
    private Vector3 recoilDir;

    private Vector3 wanderDir;
    private float wanderTimer;

    internal bool isActive => state != BehaviourState.Inactive;

    private static int formationCounter;
    private int formationSlot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        startDelay = Random.Range(0f, 2f);
        formationSlot = formationCounter % 8;
        formationCounter++;

        wantsToFlyHigh = Random.value <= chanceToFlyHigh;
        likesClimbingEnemies = Random.value <= climbOverEnemyChance;
        personalExtraFlyHeight = Random.Range(minExtraFlyHeight, maxExtraFlyHeight);
        returnToGroundDistance = Random.Range(2.5f, 7f);

        flankSide = Random.value < 0.5f ? 1f : -1f;
        flankAngle = formationSlot * (360f / 8f);

        lungeCooldownTimer.ResetRandom(0f, 10f);
        PickRandomWanderDir();
    }

    private void Start()
    {
        if (!PlayerFinder.TryAssignIfNull(ref playerTarget))
            Debug.LogWarning("EnemyController: No player found.");

        Camera cam = Camera.main;
        if (cam != null)
            playerCamera = cam.transform;
    }

    private void FixedUpdate()
    {
        if (playerTarget == null || hasHit)
            return;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        float dt = Time.fixedDeltaTime;
        aliveTime += dt;
        lungeCooldownTimer.Tick(dt);

        UpdateState(dt);
        ApplyMovement(dt);
    }

    public void SetTarget(Transform target)
    {
        playerTarget = target;
    }

    public void TakeRecoil(Vector3 hitDirection)
    {
        recoilDir = new Vector3(-hitDirection.x, 0f, -hitDirection.z).normalized;
        recoilTimer = recoilDuration;
        state = BehaviourState.Recoil;
    }

    private void UpdateState(float dt)
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (state == BehaviourState.Recoil)
        {
            recoilTimer -= dt;
            if (recoilTimer <= 0f)
                TransitionFromIdle(distToPlayer);
            return;
        }

        if (state == BehaviourState.Inactive)
        {
            startDelay -= dt;
            if (startDelay <= 0f)
                state = BehaviourState.Wander;
            return;
        }

        if (state == BehaviourState.Lunge)
        {
            UpdateLunge(dt, distToPlayer);
            return;
        }

        bool inRange = distToPlayer <= detectionRadius;
        bool lost = distToPlayer > lostRadius;

        if (inRange)
        {
            lastKnownPlayerPos = playerTarget.position;
            hasLastKnownPos = true;
        }

        if (state != BehaviourState.Wander && lost)
        {
            state = BehaviourState.Wander;
            stateTimer = 0f;
            return;
        }

        if (inRange && lungeCooldownTimer.IsReady
            && distToPlayer <= lungeTriggerDistance
            && Random.value <= lungeChance)
        {
            BeginLunge();
            return;
        }

        stateTimer -= dt;

        switch (state)
        {
            case BehaviourState.Wander:
                UpdateWander(dt);
                if (inRange)
                    TransitionFromIdle(distToPlayer);
                break;

            case BehaviourState.Chase:
            case BehaviourState.Intercept:
                if (stateTimer <= 0f)
                    TransitionFromIdle(distToPlayer);
                break;

            case BehaviourState.Flank:
                if (stateTimer <= 0f)
                {
                    state = BehaviourState.Chase;
                    stateTimer = Random.Range(3f, 8f);
                }
                else
                {
                    UpdateFlankAngle(dt);
                }
                break;
        }
    }

    private void TransitionFromIdle(float distToPlayer)
    {
        if (distToPlayer >= interceptDistance)
        {
            state = BehaviourState.Intercept;
            stateTimer = Random.Range(4f, 8f);
        }
        else if (Random.value <= flankChance)
        {
            state = BehaviourState.Flank;
            stateTimer = flankDuration + Random.Range(-3f, 3f);
        }
        else
        {
            state = BehaviourState.Chase;
            stateTimer = Random.Range(3f, 8f);
        }
    }

    private void UpdateWander(float dt)
    {
        wanderTimer -= dt;
        if (wanderTimer <= 0f)
            PickRandomWanderDir();
    }

    private void UpdateFlankAngle(float dt)
    {
        bool playerLooking = VisibilityChecker.IsTransformVisibleToCamera(
            transform, playerCamera, visibilityDotThreshold);

        float orbitDelta = flankOrbitSpeed * dt * flankSide * Mathf.Rad2Deg;

        if (playerLooking)
            orbitDelta *= 0.15f;

        flankAngle += orbitDelta;
    }

    private void PickRandomWanderDir()
    {
        Vector2 r = Random.insideUnitCircle.normalized;
        wanderDir = new Vector3(r.x, 0f, r.y);
        wanderTimer = Random.Range(3f, 7f);
    }

    private void BeginLunge()
    {
        state = BehaviourState.Lunge;
        lungeTimer = lungePrepareTime + lungeDuration + lungeExhaustTime;
        lungeDirection = (playerTarget.position - transform.position).normalized;
        lungeMoveVelocity = Vector3.zero;
        lungeCooldownTimer.Reset(lungeCooldown);
    }

    private void UpdateLunge(float dt, float distToPlayer)
    {
        lungeTimer -= dt;

        float totalTime = lungePrepareTime + lungeDuration + lungeExhaustTime;
        float elapsed = totalTime - lungeTimer;

        if (elapsed < lungePrepareTime)
        {
            Vector3 toPlayer = playerTarget.position - transform.position;
            if (toPlayer.sqrMagnitude > 0.01f)
                lungeDirection = toPlayer.normalized;
            lungeMoveVelocity = Vector3.zero;
        }
        else if (elapsed < lungePrepareTime + lungeDuration)
        {
            lungeMoveVelocity = lungeDirection * lungeSpeed;
        }
        else if (lungeTimer > 0f)
        {
            lungeMoveVelocity = Vector3.zero;
        }
        else
        {
            TransitionFromIdle(distToPlayer);
        }
    }

    private void ApplyMovement(float dt)
    {
        if (state == BehaviourState.Inactive)
            return;

        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float yVelocity = GetVerticalVelocity();

        if (state == BehaviourState.Recoil)
        {
            rb.linearVelocity = new Vector3(
                recoilDir.x * recoilForce,
                yVelocity,
                recoilDir.z * recoilForce
            );
            return;
        }

        if (state == BehaviourState.Lunge)
        {
            rb.linearVelocity = new Vector3(
                lungeMoveVelocity.x,
                yVelocity,
                lungeMoveVelocity.z
            );
            return;
        }

        Vector3 steering = ComputeSteering(distToPlayer);
        steering.y = 0f;

        float targetSpeed = GetTargetSpeed(distToPlayer);
        Vector3 targetVelocity = steering.sqrMagnitude > 0.01f
            ? steering.normalized * targetSpeed
            : Vector3.zero;

        smoothedVelocity = Vector3.MoveTowards(smoothedVelocity, targetVelocity, acceleration * dt);

        if (smoothedVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(
                new Vector3(smoothedVelocity.x, 0f, smoothedVelocity.z));
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * dt));
        }

        rb.linearVelocity = new Vector3(smoothedVelocity.x, yVelocity, smoothedVelocity.z);
    }

    private Vector3 ComputeSteering(float distToPlayer)
    {
        Vector3 toPlayer = playerTarget.position - transform.position;
        Vector3 toPlayerFlat = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;

        Vector3 primary = GetPrimaryDirection(toPlayerFlat, distToPlayer);
        Vector3 separation = GetSeparation();
        Vector3 obstacle = GetObstacleAvoidance(primary);
        Vector3 boundary = GetBoundaryPush();

        return primary
            + separation * separationStrength
            + obstacle * avoidanceStrength
            + boundary * boundaryStrength;
    }

    private Vector3 GetPrimaryDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        switch (state)
        {
            case BehaviourState.Wander: return GetWanderDirection();
            case BehaviourState.Chase: return toPlayerFlat;
            case BehaviourState.Intercept: return GetInterceptDirection(toPlayerFlat, distToPlayer);
            case BehaviourState.Flank: return GetFlankDirection(toPlayerFlat, distToPlayer);
            default: return toPlayerFlat;
        }
    }

    private Vector3 GetWanderDirection()
    {
        Vector3 flock = GetFlockDirection();

        if (hasLastKnownPos)
        {
            Vector3 toLast = lastKnownPlayerPos - transform.position;
            toLast.y = 0f;

            if (toLast.magnitude < 2f)
            {
                hasLastKnownPos = false;
                PickRandomWanderDir();
            }
            else
            {
                Vector3 combined = toLast.normalized + flock * flockStrength;
                return combined.sqrMagnitude > 0.01f ? combined.normalized : toLast.normalized;
            }
        }

        Vector3 result = wanderDir + flock * flockStrength;
        return result.sqrMagnitude > 0.01f ? result.normalized : wanderDir;
    }

    private Vector3 GetInterceptDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        Rigidbody playerRb = playerTarget.GetComponent<Rigidbody>();
        Vector3 playerVel = playerRb != null ? new Vector3(
            playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z) : Vector3.zero;

        float timeToReach = distToPlayer / Mathf.Max(moveSpeed * chaseSpeedMultiplier, 0.1f);
        Vector3 predicted = playerTarget.position + playerVel * timeToReach * interceptLookAhead;

        Vector3 toIntercept = predicted - transform.position;
        toIntercept.y = 0f;

        return toIntercept.sqrMagnitude > 0.01f ? toIntercept.normalized : toPlayerFlat;
    }

    private Vector3 GetFlankDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        if (distToPlayer <= attackDistance)
            return toPlayerFlat;

        Vector3 orbitOffset = Quaternion.Euler(0f, flankAngle, 0f) * Vector3.forward * flankOrbitRadius;
        Vector3 targetPos = playerTarget.position + orbitOffset;
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;

        return toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : toPlayerFlat;
    }

    private Vector3 GetSeparation()
    {
        Collider[] nearby = Physics.OverlapSphere(
            transform.position, separationRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        Vector3 push = Vector3.zero;

        foreach (Collider col in nearby)
        {
            if (col.attachedRigidbody == rb)
                continue;

            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float dist = away.magnitude;

            if (dist < 0.01f)
                continue;

            push += away.normalized * (1f - Mathf.Clamp01(dist / separationRadius));
        }

        return push;
    }

    private Vector3 GetObstacleAvoidance(Vector3 forward)
    {
        if (forward.sqrMagnitude < 0.01f)
            return Vector3.zero;

        Vector3 origin = transform.position + Vector3.up * 0.7f;
        float[] angles = { 0f, -35f, 35f, -70f, 70f };

        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;

            if (!Physics.SphereCast(origin, avoidanceRadius, dir, out _,
                avoidanceDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                return angle == 0f ? Vector3.zero : dir.normalized;
            }
        }

        return -forward;
    }

    private Vector3 GetBoundaryPush()
    {
        Vector3 push = Vector3.zero;

        Vector3[] dirs = {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            (Vector3.forward + Vector3.right).normalized,
            (Vector3.forward + Vector3.left).normalized,
            (Vector3.back + Vector3.right).normalized,
            (Vector3.back + Vector3.left).normalized
        };

        foreach (Vector3 dir in dirs)
        {
            Vector3 checkOrigin = transform.position + dir * boundaryLookAhead + Vector3.up * 0.5f;

            if (!Physics.Raycast(checkOrigin, Vector3.down,
                heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                push -= dir;
            }
        }

        return push.sqrMagnitude > 0.01f ? push.normalized : Vector3.zero;
    }

    private Vector3 GetFlockDirection()
    {
        Collider[] nearby = Physics.OverlapSphere(
            transform.position, flockRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        Vector3 centerOfMass = Vector3.zero;
        Vector3 avgVelocity = Vector3.zero;
        int count = 0;

        foreach (Collider col in nearby)
        {
            if (col.attachedRigidbody == rb)
                continue;

            EnemyController other = col.GetComponentInParent<EnemyController>();
            if (other == null || other.state != BehaviourState.Wander)
                continue;

            centerOfMass += col.transform.position;
            if (col.attachedRigidbody != null)
                avgVelocity += col.attachedRigidbody.linearVelocity;
            count++;
        }

        if (count < flockMinGroupSize)
            return Vector3.zero;

        centerOfMass /= count;
        avgVelocity /= count;

        Vector3 toCom = centerOfMass - transform.position;
        toCom.y = 0f;
        avgVelocity.y = 0f;

        Vector3 result = toCom.normalized * 0.7f + avgVelocity.normalized * 0.3f;
        return result.sqrMagnitude > 0.01f ? result.normalized : Vector3.zero;
    }

    private float GetTargetSpeed(float distToPlayer)
    {
        float baseSpeed = state == BehaviourState.Wander
            ? wanderSpeed
            : moveSpeed * chaseSpeedMultiplier;

        return baseSpeed * GetDistanceBoost(distToPlayer) * GetFatigueMultiplier();
    }

    private float GetDistanceBoost(float distToPlayer)
    {
        if (distToPlayer <= boostStartDistance)
            return 1f;

        float t = Mathf.InverseLerp(boostStartDistance, boostMaxDistance, distToPlayer);
        return Mathf.Lerp(1f, maxBoostMultiplier, t);
    }

    private float GetFatigueMultiplier()
    {
        if (aliveTime < fatigueStartTime)
            return 1f;

        float t = Mathf.Clamp01((aliveTime - fatigueStartTime) / fatigueDuration);
        return Mathf.Lerp(1f, fatigueMinMultiplier, t);
    }

    private float GetVerticalVelocity()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float targetY = GetTargetFlyingHeight(distToPlayer);
        float diff = targetY - transform.position.y;
        return Mathf.Clamp(diff * verticalSpeed, -verticalSpeed, verticalSpeed);
    }

    private float GetTargetFlyingHeight(float distToPlayer)
    {
        float surfaceY = GetSurfaceY();
        float restHeight = surfaceY + preferredFloatHeight;
        float maxAllowedHeight = surfaceY + maxFloatHeight;

        float desiredHeight = restHeight;

        if (distToPlayer > returnToGroundDistance)
        {
            if (wantsToFlyHigh)
                desiredHeight = restHeight + personalExtraFlyHeight;

            if (distToPlayer <= heightFollowDistance)
                desiredHeight = Mathf.Max(desiredHeight, playerTarget.position.y);

            if (likesClimbingEnemies)
            {
                float climbHeight = GetEnemyClimbHeight();
                if (climbHeight > desiredHeight)
                    desiredHeight = climbHeight;
            }
        }

        return Mathf.Clamp(desiredHeight, restHeight, maxAllowedHeight);
    }

    private float GetEnemyClimbHeight()
    {
        Vector3 forward = transform.forward;
        Vector3 checkCenter = transform.position + forward * climbCheckDistance + Vector3.up * 0.8f;

        Collider[] nearby = Physics.OverlapSphere(
            checkCenter, separationRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        float highestY = float.MinValue;

        foreach (Collider col in nearby)
        {
            if (col.attachedRigidbody == rb)
                continue;

            highestY = Mathf.Max(highestY, col.bounds.max.y);
        }

        return highestY == float.MinValue ? float.MinValue : highestY + climbOverEnemyHeight;
    }

    private float GetSurfaceY()
    {
        Vector3 origin = transform.position + Vector3.up * 10f;

        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
            heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : 0f;
    }

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
            Vector3 dir = health.transform.position - transform.position;
            dir.y = 0f;
            playerController.ApplyKnockback(dir, knockbackForce, upwardKnockbackForce);
        }

        Destroy(gameObject);
    }
}