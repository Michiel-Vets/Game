using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────────────────
    // State machine
    // ────────────────────────────────────────────────────────────────────────────
    private enum BehaviourState
    {
        Inactive,
        Chase,
        Flank,
        Intercept,
        Lunge,
        Recoil,
        Weakened,   // spotted by flashlight but light moved away — retreats slowly
        Fleeing,    // just dealt damage — races away before vanishing
        Dying,      // flashlight held on it for killTime — dissolves then destroys
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Static — reset on every play-mode entry
    // ────────────────────────────────────────────────────────────────────────────
    private static int formationCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => formationCounter = 0;

    // ────────────────────────────────────────────────────────────────────────────
    // Inspector fields
    // ────────────────────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;
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

    [Header("Distance Speed Boost")]
    [SerializeField] private float boostStartDistance = 15f;
    [SerializeField] private float boostMaxDistance = 50f;
    [SerializeField] private float maxBoostMultiplier = 2f;

    [Header("Surround & Flank")]
    [SerializeField, Range(0f, 1f)] private float flankChance = 0.45f;
    [SerializeField] private float flankDuration = 12f;
    [SerializeField] private float flankOrbitRadius = 5f;
    [SerializeField] private float flankOrbitSpeed = 1.2f;
    [SerializeField] private float flankSideFlipInterval = 4f;
    [SerializeField, Range(0f, 1f)] private float visibilityDotThreshold = 0.5f;

    [Header("Intercept")]
    [SerializeField] private float interceptDistance = 20f;
    [SerializeField] private float minInterceptDistance = 10f;
    [SerializeField, Range(0f, 1f)] private float interceptChance = 0.12f;
    [SerializeField] private float interceptLookAhead = 0.6f;
    [SerializeField] private float interceptMinDuration = 1.5f;
    [SerializeField] private float interceptMaxDuration = 3f;

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

    [Header("Height Variation")]
    [SerializeField] private float heightChangeInterval = 3f;
    [SerializeField] private float heightChangeIntervalVariance = 2f;
    [SerializeField] private float minWanderHeight = 1f;
    [SerializeField] private float maxWanderHeight = 6f;
    [SerializeField] private float chaseHeightVariance = 2f;

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

    [Header("Fleeing (after dealing damage)")]
    [SerializeField] private float fleeSpeed = 8f;
    [SerializeField] private float fleeDuration = 2.5f;

    [Header("Weakened (flashlight spotted then lost)")]
    [SerializeField] private float weakenedSpeed = 2f;
    [SerializeField] private float weakenedDuration = 6f;
    [SerializeField] private float weakenedRetreatDistance = 12f;

    [Header("Flashlight / Kill")]
    [SerializeField] private float flashlightKillTime = 2f;
    [SerializeField] private float dissolveSpeed = 1.5f;

    [Header("Crowd Spreading / Surround")]
    [SerializeField] private float crowdCheckInterval = 0.7f;
    [SerializeField] private float crowdSpreadRadius = 8f;
    [SerializeField] private int maxNearbyAttackersBeforeSpread = 5;
    [SerializeField, Range(0f, 1f)] private float crowdSpreadChance = 0.65f;
    [SerializeField] private float crowdSpreadMinDuration = 4f;
    [SerializeField] private float crowdSpreadMaxDuration = 8f;
    [SerializeField] private float crowdSurroundRadius = 7f;
    [SerializeField] private float crowdAngleJitter = 35f;


    // ────────────────────────────────────────────────────────────────────────────
    // Runtime state
    // ────────────────────────────────────────────────────────────────────────────
    private Rigidbody rb;
    private Transform playerTarget;
    private Transform playerCamera;

    private BehaviourState state = BehaviourState.Inactive;

    // Personality — randomised once in Awake
    private bool wantsToFlyHigh;
    private float personalExtraFlyHeight;
    private bool likesClimbingEnemies;
    private float returnToGroundDistance;
    private float aggressionBias; // 0 = timid, 1 = reckless

    private float startDelay;
    private float aliveTime;
    private float stateTimer;
    private bool hasHit;

    private Vector3 smoothedVelocity;

    private float flankAngle;
    private float flankSide;
    private float flankSideTimer;
    private int formationSlot;

    private float lungeTimer;
    private Vector3 lungeDirection;
    private Vector3 lungeMoveVelocity;
    private readonly Cooldown lungeCooldownTimer = new Cooldown();

    private float recoilTimer;
    private Vector3 recoilDir;

    private float currentTargetHeight;
    private float heightTimer;

    // Flashlight
    private float flashlightExposureTime;
    private bool isInFlashlightBeam;

    // Flee direction (set when ghost touches player)
    private Vector3 fleeDirection;

    // ────────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ────────────────────────────────────────────────────────────────────────────
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
        personalExtraFlyHeight = Random.Range(minExtraFlyHeight, maxExtraFlyHeight);
        likesClimbingEnemies = Random.value <= climbOverEnemyChance;
        returnToGroundDistance = Random.Range(2.5f, 7f);
        aggressionBias = Random.value;

        flankSide = Random.value < 0.5f ? 1f : -1f;
        flankAngle = formationSlot * (360f / 8f);
        flankSideTimer = flankSideFlipInterval * Random.Range(0.5f, 1.5f);

        lungeCooldownTimer.ResetRandom(0f, 10f);
        currentTargetHeight = Random.Range(minWanderHeight, maxWanderHeight);
        heightTimer = Random.Range(0f, heightChangeInterval);
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

        UpdateFlashlightExposure(dt);

        heightTimer -= dt;
        if (heightTimer <= 0f)
        {
            float interval = heightChangeInterval
                + Random.Range(-heightChangeIntervalVariance, heightChangeIntervalVariance);
            heightTimer = Mathf.Max(1f, interval);

            currentTargetHeight = preferredFloatHeight
                + Random.Range(-chaseHeightVariance, chaseHeightVariance);
            currentTargetHeight = Mathf.Clamp(currentTargetHeight, minWanderHeight, maxFloatHeight);
        }

        UpdateState(dt);
        ApplyMovement(dt);

        // Reset beam flag — flashlight must re-confirm every frame
        isInFlashlightBeam = false;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Public API
    // ────────────────────────────────────────────────────────────────────────────
    public void SetTarget(Transform target) => playerTarget = target;

    public void TakeRecoil(Vector3 hitDirection)
    {
        recoilDir = new Vector3(-hitDirection.x, 0f, -hitDirection.z).normalized;
        recoilTimer = recoilDuration;
        state = BehaviourState.Recoil;
    }

    /// <summary>
    /// Called every frame by the Flashlight script while its beam hits this ghost.
    /// The flashlight should do a Physics.SphereCast / raycast and call this on hit.
    /// </summary>
    public void ReceiveFlashlightHit()
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        isInFlashlightBeam = true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Flashlight exposure
    // ────────────────────────────────────────────────────────────────────────────
    private void UpdateFlashlightExposure(float dt)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        if (isInFlashlightBeam)
        {
            flashlightExposureTime += dt;

            if (flashlightExposureTime >= flashlightKillTime)
                BeginDying();
        }
        else
        {
            // Light left before the kill threshold — ghost is spooked but survives
            if (flashlightExposureTime > 0f && state != BehaviourState.Weakened)
                EnterWeakened();

            flashlightExposureTime = 0f;
        }
    }

    private void EnterWeakened()
    {
        if (state == BehaviourState.Dying) return;

        state = BehaviourState.Weakened;
        // Aggressive ghosts recover from weaken faster
        stateTimer = weakenedDuration * Mathf.Lerp(1.5f, 0.6f, aggressionBias);
    }

    private void BeginDying()
    {
        state = BehaviourState.Dying;
        rb.linearVelocity = Vector3.zero;
    }

    private void BeginFleeing(Vector3 fromPosition)
    {
        Vector3 away = transform.position - fromPosition;
        away.y = 0f;
        fleeDirection = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;

        state = BehaviourState.Fleeing;
        stateTimer = fleeDuration;
        lungeMoveVelocity = Vector3.zero;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // State machine
    // ────────────────────────────────────────────────────────────────────────────
    private void UpdateState(float dt)
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // ── Dying: shrink and destroy ──
        if (state == BehaviourState.Dying)
        {
            transform.localScale = Vector3.MoveTowards(
                transform.localScale, Vector3.zero, dissolveSpeed * dt);

            if (transform.localScale.sqrMagnitude < 0.001f)
                Destroy(gameObject);
            return;
        }

        // ── Fleeing: race away after dealing damage, then dissolve ──
        if (state == BehaviourState.Fleeing)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f)
                BeginDying();
            return;
        }

        // ── Weakened: retreat until timer expires, then recover ──
        if (state == BehaviourState.Weakened)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f)
                TransitionToAttack(distToPlayer);
            return;
        }

        if (state == BehaviourState.Recoil)
        {
            recoilTimer -= dt;
            if (recoilTimer <= 0f)
                TransitionToAttack(distToPlayer);
            return;
        }

        if (state == BehaviourState.Inactive)
        {
            startDelay -= dt;
            if (startDelay <= 0f)
                TransitionToAttack(distToPlayer);
            return;
        }

        if (state == BehaviourState.Lunge)
        {
            UpdateLunge(dt, distToPlayer);
            return;
        }

        // Personality-scaled lunge thresholds
        float effectiveLungeTrigger = lungeTriggerDistance * Mathf.Lerp(0.6f, 1.4f, aggressionBias);
        float effectiveLungeChance = lungeChance * Mathf.Lerp(0.4f, 1.6f, aggressionBias);

        if (lungeCooldownTimer.IsReady
            && distToPlayer <= effectiveLungeTrigger
            && Random.value <= effectiveLungeChance)
        {
            BeginLunge();
            return;
        }

        stateTimer -= dt;

        switch (state)
        {
            case BehaviourState.Chase:
            case BehaviourState.Intercept:
                if (stateTimer <= 0f)
                    TransitionToAttack(distToPlayer);
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

    private void TransitionToAttack(float distToPlayer)
    {
        bool canIntercept = distToPlayer >= minInterceptDistance && distToPlayer <= interceptDistance;
        bool choosesIntercept = canIntercept && Random.value <= interceptChance;

        if (choosesIntercept)
        {
            state = BehaviourState.Intercept;
            stateTimer = Random.Range(interceptMinDuration, interceptMaxDuration);
        }
        else if (Random.value > aggressionBias && Random.value <= flankChance)
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

    // ────────────────────────────────────────────────────────────────────────────
    // Lunge
    // ────────────────────────────────────────────────────────────────────────────
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
            if (toPlayer.sqrMagnitude > 0.01f) lungeDirection = toPlayer.normalized;
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
            TransitionToAttack(distToPlayer);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Flank — direction flips periodically and faster when player is looking
    // ────────────────────────────────────────────────────────────────────────────
    private void UpdateFlankAngle(float dt)
    {
        bool playerLooking = VisibilityChecker.IsTransformVisibleToCamera(
            transform, playerCamera, visibilityDotThreshold);

        // Flip direction on a timer; accelerate the flip when the player looks at it
        flankSideTimer -= playerLooking ? dt * 2f : dt;
        if (flankSideTimer <= 0f)
        {
            flankSide = -flankSide;
            flankSideTimer = flankSideFlipInterval * Random.Range(0.6f, 1.4f);
        }

        float orbitDelta = flankOrbitSpeed * dt * flankSide * Mathf.Rad2Deg;
        if (playerLooking) orbitDelta *= 0.15f; // nearly freeze when looked at
        flankAngle += orbitDelta;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Movement
    // ────────────────────────────────────────────────────────────────────────────
    private void ApplyMovement(float dt)
    {
        if (state == BehaviourState.Inactive || state == BehaviourState.Dying)
            return;

        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float yVelocity = GetVerticalVelocity();

        if (state == BehaviourState.Recoil)
        {
            rb.linearVelocity = new Vector3(
                recoilDir.x * recoilForce, yVelocity, recoilDir.z * recoilForce);
            return;
        }

        if (state == BehaviourState.Lunge)
        {
            rb.linearVelocity = lungeMoveVelocity;
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
        Vector3 sep = GetSeparation();
        Vector3 obstacle = GetObstacleAvoidance(primary);
        Vector3 boundary = GetBoundaryPush();

        return primary
            + sep * separationStrength
            + obstacle * avoidanceStrength
            + boundary * boundaryStrength;
    }

    private Vector3 GetPrimaryDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        switch (state)
        {
            case BehaviourState.Chase: return toPlayerFlat;
            case BehaviourState.Intercept: return GetInterceptDirection(toPlayerFlat, distToPlayer);
            case BehaviourState.Flank: return GetFlankDirection(toPlayerFlat, distToPlayer);
            case BehaviourState.Weakened: return GetWeakenedDirection(toPlayerFlat, distToPlayer);
            case BehaviourState.Fleeing: return fleeDirection;
            default: return toPlayerFlat;
        }
    }

    private Vector3 GetInterceptDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        Rigidbody playerRb = playerTarget.GetComponent<Rigidbody>();
        Vector3 playerVel = playerRb != null
            ? new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z)
            : Vector3.zero;

        float timeToReach = distToPlayer / Mathf.Max(moveSpeed * chaseSpeedMultiplier, 0.1f);
        Vector3 toIntercept = playerTarget.position
            + playerVel * timeToReach * interceptLookAhead
            - transform.position;
        toIntercept.y = 0f;
        return toIntercept.sqrMagnitude > 0.01f ? toIntercept.normalized : toPlayerFlat;
    }

    private Vector3 GetFlankDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        if (distToPlayer <= attackDistance) return toPlayerFlat;

        Vector3 orbitOffset = Quaternion.Euler(0f, flankAngle, 0f) * Vector3.forward * flankOrbitRadius;
        Vector3 toTarget = playerTarget.position + orbitOffset - transform.position;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : toPlayerFlat;
    }

    /// <summary>
    /// Weakened ghost keeps its distance: retreats if too close, drifts sideways otherwise.
    /// </summary>
    private Vector3 GetWeakenedDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        if (distToPlayer < weakenedRetreatDistance)
            return -toPlayerFlat;

        // Far enough away — drift sideways so it doesn't just hover in place
        return Vector3.Cross(toPlayerFlat, Vector3.up) * flankSide;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Steering helpers
    // ────────────────────────────────────────────────────────────────────────────
    private Vector3 GetSeparation()
    {
        Collider[] nearby = Physics.OverlapSphere(
            transform.position, separationRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        Vector3 push = Vector3.zero;
        foreach (Collider col in nearby)
        {
            if (col.attachedRigidbody == rb) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < 0.01f) continue;
            push += away.normalized * (1f - Mathf.Clamp01(dist / separationRadius));
        }
        return push;
    }

    private Vector3 GetObstacleAvoidance(Vector3 forward)
    {
        if (forward.sqrMagnitude < 0.01f) return Vector3.zero;

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
        Vector3[] dirs =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            (Vector3.forward + Vector3.right).normalized,
            (Vector3.forward + Vector3.left).normalized,
            (Vector3.back   + Vector3.right).normalized,
            (Vector3.back   + Vector3.left).normalized
        };

        foreach (Vector3 dir in dirs)
        {
            Vector3 origin = transform.position + dir * boundaryLookAhead + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down,
                heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                push -= dir;
            }
        }
        return push.sqrMagnitude > 0.01f ? push.normalized : Vector3.zero;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Speed & height
    // ────────────────────────────────────────────────────────────────────────────
    private float GetTargetSpeed(float distToPlayer)
    {
        if (state == BehaviourState.Weakened) return weakenedSpeed;
        if (state == BehaviourState.Fleeing) return fleeSpeed;

        float baseSpeed = moveSpeed * chaseSpeedMultiplier;
        return baseSpeed * GetDistanceBoost(distToPlayer) * GetFatigueMultiplier();
    }

    private float GetDistanceBoost(float distToPlayer)
    {
        if (distToPlayer <= boostStartDistance) return 1f;
        float t = Mathf.InverseLerp(boostStartDistance, boostMaxDistance, distToPlayer);
        return Mathf.Lerp(1f, maxBoostMultiplier, t);
    }

    private float GetFatigueMultiplier()
    {
        if (aliveTime < fatigueStartTime) return 1f;
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
        float maxHeight = surfaceY + maxFloatHeight;

        float desiredHeight = surfaceY + currentTargetHeight
            + (wantsToFlyHigh ? personalExtraFlyHeight : 0f);

        if (distToPlayer <= returnToGroundDistance)
            desiredHeight = restHeight;
        else if (distToPlayer <= heightFollowDistance)
            desiredHeight = Mathf.Max(desiredHeight, playerTarget.position.y);

        if (likesClimbingEnemies)
        {
            float climbHeight = GetEnemyClimbHeight();
            if (climbHeight > desiredHeight) desiredHeight = climbHeight;
        }

        return Mathf.Clamp(desiredHeight, restHeight, maxHeight);
    }

    private float GetEnemyClimbHeight()
    {
        Vector3 checkCenter = transform.position
            + transform.forward * climbCheckDistance + Vector3.up * 0.8f;

        Collider[] nearby = Physics.OverlapSphere(
            checkCenter, separationRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        float highestY = float.MinValue;
        foreach (Collider col in nearby)
        {
            if (col.attachedRigidbody == rb) continue;
            highestY = Mathf.Max(highestY, col.bounds.max.y);
        }
        return highestY == float.MinValue ? float.MinValue : highestY + climbOverEnemyHeight;
    }

    private float GetSurfaceY()
    {
        Vector3 origin = transform.position + Vector3.up * 10f;
        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
            heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : 0f;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Utilities
    // ────────────────────────────────────────────────────────────────────────────
    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Combat
    // ────────────────────────────────────────────────────────────────────────────
    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        HealthController health = collision.gameObject.GetComponentInParent<HealthController>();
        if (health == null || !health.CompareTag("Player")) return;

        hasHit = true;
        health.TakeDamage(health.MaxHealth * damagePercentage);

        PlayerController pc = health.GetComponent<PlayerController>();
        if (pc != null)
        {
            Vector3 dir = health.transform.position - transform.position;
            dir.y = 0f;
            pc.ApplyKnockback(dir, knockbackForce, upwardKnockbackForce);
        }

        // Ghost flees after dealing damage instead of instantly dying
        BeginFleeing(health.transform.position);
    }
}