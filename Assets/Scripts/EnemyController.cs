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

    [Header("Obstacle Clearance (flying over)")]
    [Tooltip("How far ahead to scan for obstacles to fly over.")]
    [SerializeField] private float obstacleScanDistance = 6f;
    [Tooltip("Extra height added on top of a detected obstacle before flying over it.")]
    [SerializeField] private float obstacleOvershootHeight = 1.2f;
    [Tooltip("How quickly the enemy rises to clear an obstacle (blended with normal vertical speed).")]
    [SerializeField] private float obstacleLiftSpeed = 6f;
    [Tooltip("Horizontal radius of the forward scan capsule used to detect obstacles ahead.")]
    [SerializeField] private float obstacleScanRadius = 0.6f;

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
    [SerializeField] private float weakenedSpeed = 1.5f;
    [SerializeField] private float weakenedRetreatDistance = 30f;

    [Header("Flashlight / Health")]
    [Tooltip("Seconden om de enemy van vol naar dood te belichten.")]
    [SerializeField] private float flashlightKillTime = 2f;
    [Tooltip("Seconden voordat de enemy volledig herstelt als hij niet belicht wordt en ver genoeg is.")]
    [SerializeField] private float healTime = 5f;
    [Tooltip("Minimale afstand tot de speler voordat de enemy begint te herstellen.")]
    [SerializeField] private float healMinDistance = 30f;
    [Tooltip("Snelheid waarmee de scale verandert (zichtbaar krimpen/groeien).")]
    [SerializeField] private float scaleChangeSpeed = 2f;

    [Header("Aggression Scaling (set on prefab, injected by spawner)")]
    [Tooltip("Minimum aggression floor at the very start of the game (0 = fully timid, 1 = fully reckless).")]
    [SerializeField, Range(0f, 1f)] private float aggressionAtStart = 0f;
    [Tooltip("Maximum aggression floor reached after aggressionRampDuration seconds (0–1).")]
    [SerializeField, Range(0f, 1f)] private float aggressionAtEnd = 0.7f;
    [Tooltip("Time in seconds it takes to go from aggressionAtStart to aggressionAtEnd.")]
    [SerializeField] private float aggressionRampDuration = 180f;

    [Tooltip("Move speed multiplier at minimum aggression.")]
    [SerializeField] private float speedMultiplierMin = 1.0f;
    [Tooltip("Move speed multiplier at maximum aggression.")]
    [SerializeField] private float speedMultiplierMax = 1.6f;

    [Tooltip("Damage multiplier at minimum aggression.")]
    [SerializeField] private float damageMultiplierMin = 0.5f;
    [Tooltip("Damage multiplier at maximum aggression.")]
    [SerializeField] private float damageMultiplierMax = 2.0f;

    [Tooltip("Lunge trigger distance multiplier at minimum aggression.")]
    [SerializeField] private float lungeDistanceMultiplierMin = 0.7f;
    [Tooltip("Lunge trigger distance multiplier at maximum aggression.")]
    [SerializeField] private float lungeDistanceMultiplierMax = 1.8f;

    [Tooltip("Lunge speed multiplier at minimum aggression.")]
    [SerializeField] private float lungeSpeedMultiplierMin = 0.8f;
    [Tooltip("Lunge speed multiplier at maximum aggression.")]
    [SerializeField] private float lungeSpeedMultiplierMax = 1.4f;

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

    // Flashlight / Health (0 = vol leven, 1 = dood)
    private float flashlightDamage = 0f;   // 0..1, groeit bij belichting, krimpt bij herstel
    private bool isInFlashlightBeam;
    private Vector3 originalScale;

    // Flee direction (set when ghost touches player)
    private Vector3 fleeDirection;

    // Injected by EnemySpawner — used to scale aggression on spawn
    private float gameTimeSurvived;

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
        // Base aggression is random, but clamped — actual scaling applied in InitStats() after SetSurvivedTime
        aggressionBias = Random.value;

        flankSide = Random.value < 0.5f ? 1f : -1f;
        flankAngle = formationSlot * (360f / 8f);
        flankSideTimer = flankSideFlipInterval * Random.Range(0.5f, 1.5f);

        lungeCooldownTimer.ResetRandom(0f, 10f);
        currentTargetHeight = Random.Range(minWanderHeight, maxWanderHeight);
        heightTimer = Random.Range(0f, heightChangeInterval);
        originalScale = transform.localScale;
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

    /// <summary>
    /// Called by EnemySpawner right after instantiation.
    /// The longer the game has been running, the higher the minimum aggression floor.
    /// All thresholds and multipliers are configurable in the Inspector.
    /// </summary>
    public void SetSurvivedTime(float survivedTime)
    {
        gameTimeSurvived = survivedTime;

        // Aggression floor interpolates from aggressionAtStart → aggressionAtEnd over aggressionRampDuration
        float t = aggressionRampDuration > 0f
            ? Mathf.Clamp01(survivedTime / aggressionRampDuration)
            : 1f;
        float aggressionFloor = Mathf.Lerp(aggressionAtStart, aggressionAtEnd, t);

        // Random personality bias is kept if it's already more aggressive than the floor
        aggressionBias = Mathf.Max(aggressionBias, aggressionFloor);

        ApplyAggressionStats();
    }

    /// <summary>
    /// Scales move speed, damage and lunge range based on aggressionBias.
    /// All multiplier ranges are configurable in the Inspector.
    /// Called once after aggressionBias is finalised.
    /// </summary>
    private void ApplyAggressionStats()
    {
        moveSpeed *= Mathf.Lerp(speedMultiplierMin, speedMultiplierMax, aggressionBias);
        damagePercentage *= Mathf.Lerp(damageMultiplierMin, damageMultiplierMax, aggressionBias);
        lungeTriggerDistance *= Mathf.Lerp(lungeDistanceMultiplierMin, lungeDistanceMultiplierMax, aggressionBias);
        lungeSpeed *= Mathf.Lerp(lungeSpeedMultiplierMin, lungeSpeedMultiplierMax, aggressionBias);
    }

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
    // Flashlight / Health systeem
    // flashlightDamage loopt van 0 (vol) tot 1 (dood).
    // Scale = originalScale * (1 - flashlightDamage), zodat de enemy zichtbaar krimpt.
    // Als de enemy niet belicht wordt en ver genoeg is herstelt hij langzaam.
    // ────────────────────────────────────────────────────────────────────────────
    private void UpdateFlashlightExposure(float dt)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        if (isInFlashlightBeam)
        {
            // Schade opbouwen terwijl de zaklamp schijnt
            flashlightDamage += dt / flashlightKillTime;
            flashlightDamage = Mathf.Clamp01(flashlightDamage);

            // Weakened state zetten zodra de enemy belicht wordt
            if (state != BehaviourState.Weakened)
                EnterWeakened();

            // Dood bij vol schade
            if (flashlightDamage >= 1f)
                BeginDying();
        }
        else
        {
            // Niet belicht: herstel als de enemy ver genoeg van de speler is
            if (state == BehaviourState.Weakened && flashlightDamage > 0f && playerTarget != null)
            {
                float dist = Vector3.Distance(transform.position, playerTarget.position);
                if (dist >= healMinDistance)
                {
                    flashlightDamage -= dt / healTime;
                    flashlightDamage = Mathf.Max(0f, flashlightDamage);

                    // Volledig hersteld: terug naar aanval
                    if (flashlightDamage <= 0f)
                        TransitionToAttack(dist);
                }
            }
        }

        // Scale altijd bijwerken op basis van huidige schade
        ApplyDamageScale();
    }

    private void ApplyDamageScale()
    {
        float healthFraction = 1f - flashlightDamage;
        Vector3 targetScale = originalScale * healthFraction;
        transform.localScale = Vector3.MoveTowards(
            transform.localScale, targetScale, scaleChangeSpeed * Time.fixedDeltaTime);
    }

    private void EnterWeakened()
    {
        if (state == BehaviourState.Dying) return;
        state = BehaviourState.Weakened;
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

        // ── Dying: scale krimpt via ApplyDamageScale, destroy als klein genoeg ──
        if (state == BehaviourState.Dying)
        {
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

        // ── Weakened: vluchten — herstel en state change gebeurt in UpdateFlashlightExposure ──
        if (state == BehaviourState.Weakened)
            return;

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
        // Altijd wegvluchten van de speler tot de herstelafstand bereikt is
        if (distToPlayer < weakenedRetreatDistance)
            return -toPlayerFlat;

        // Ver genoeg: stilstaan zodat herstel kan beginnen
        return Vector3.zero;
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
        if (state == BehaviourState.Weakened)
        {
            // Hoe meer schade, hoe trager — bij vol schade bijna stilstaand
            float speedFraction = 1f - (flashlightDamage * 0.85f);
            return weakenedSpeed * speedFraction;
        }
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

        // Use the faster lift speed when clearing an obstacle, normal vertical speed otherwise
        float speed = IsObstacleAhead() ? obstacleLiftSpeed : verticalSpeed;
        return Mathf.Clamp(diff * speed, -verticalSpeed, speed);
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

        // Fly over any obstacle directly ahead
        float clearanceHeight = GetObstacleClearHeight();
        if (clearanceHeight > desiredHeight)
            desiredHeight = clearanceHeight;

        return Mathf.Clamp(desiredHeight, restHeight, maxHeight);
    }

    /// <summary>
    /// Scans ahead in the movement direction for obstacles and returns the Y the
    /// enemy must reach to clear the top of the obstacle, or float.MinValue if clear.
    /// </summary>
    private float GetObstacleClearHeight()
    {
        // Use current horizontal velocity as the look-ahead direction; fall back to facing direction
        Vector3 moveDir = new Vector3(smoothedVelocity.x, 0f, smoothedVelocity.z);
        if (moveDir.sqrMagnitude < 0.01f)
            moveDir = new Vector3(transform.forward.x, 0f, transform.forward.z);
        moveDir.Normalize();

        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // SphereCast forward to find the nearest obstacle
        if (!Physics.SphereCast(origin, obstacleScanRadius, moveDir, out RaycastHit hit,
                obstacleScanDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
            return float.MinValue;

        // Return the top of the obstacle + clearance margin
        return hit.collider.bounds.max.y + obstacleOvershootHeight;
    }

    /// <summary>
    /// Returns true when there is an obstacle directly ahead that requires climbing.
    /// Used to select the faster lift speed.
    /// </summary>
    private bool IsObstacleAhead()
    {
        Vector3 moveDir = new Vector3(smoothedVelocity.x, 0f, smoothedVelocity.z);
        if (moveDir.sqrMagnitude < 0.01f)
            moveDir = new Vector3(transform.forward.x, 0f, transform.forward.z);
        moveDir.Normalize();

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        return Physics.SphereCast(origin, obstacleScanRadius, moveDir, out _,
            obstacleScanDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
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

        // Check ground
        float groundY = Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit,
            heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore)
            ? groundHit.point.y
            : 0f;

        // Also check obstacles directly below — enemy should float above them too
        float obstacleY = Physics.Raycast(origin, Vector3.down, out RaycastHit obstacleHit,
            heightRaycastDistance, obstacleLayers, QueryTriggerInteraction.Ignore)
            ? obstacleHit.point.y
            : float.MinValue;

        // Return the highest surface beneath the enemy
        return Mathf.Max(groundY, obstacleY);
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

        // Enemy vanishes instantly after dealing damage
        Destroy(gameObject);
    }
}