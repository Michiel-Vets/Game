using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    private enum BehaviourState
    {
        Inactive, Chase, Flank, Intercept, Lunge, Recoil,
        Weakened, Fleeing, Dying,
    }

    private static int formationCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => formationCounter = 0;

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

    [Header("Obstacle Clearance")]
    [SerializeField] private float obstacleScanDistance = 6f;
    [SerializeField] private float obstacleOvershootHeight = 1.2f;
    [SerializeField] private float obstacleLiftSpeed = 6f;
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

    [Header("Death Animation")]
    [SerializeField] private float deathLaunchSpeed = 16f;
    [SerializeField] private float deathSpinSpeed = 360f;
    [SerializeField] private float deathRiseDuration = 1.8f;

    [Header("Weakened Shrink & Fall")]
    [SerializeField, Range(0.1f, 0.9f)] private float weakenedMinScaleFraction = 0.3f;
    [SerializeField] private float weakenedMinFloatHeight = 0.15f;
    [SerializeField] private float weakenedFallSpeed = 6f;

    [Header("Combat")]
    [SerializeField] private float attackDistance = 2.2f;
    [SerializeField, Range(0f, 1f)] private float damagePercentage = 0.1f;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float upwardKnockbackForce = 2f;

    [Header("Fleeing")]
    [SerializeField] private float fleeSpeed = 8f;
    [SerializeField] private float fleeDuration = 2.5f;

    [Header("Weakened")]
    [SerializeField] private float weakenedSpeed = 1.5f;
    [SerializeField] private float weakenedRetreatDistance = 30f;

    [Header("Flashlight / Health")]
    [SerializeField] private float flashlightKillTime = 2f;
    [SerializeField] private float healTime = 5f;
    [SerializeField] private float partialHealTimeMultiplier = 2.5f;
    [SerializeField] private float scaleChangeSpeed = 2f;

    [Header("Beam Evasion")]
    [SerializeField] private float beamEvasionStrength = 6f;
    [SerializeField] private float beamEvasionRadius = 3f;
    [SerializeField, Range(0f, 1f)] private float beamEvasionChance = 0.85f;

    [Header("Aggression Scaling")]
    [SerializeField] private float aggressionSpreadDuration = 300f;
    [SerializeField] private float speedMultiplierMin = 0.4f;
    [SerializeField] private float speedMultiplierMax = 2.8f;
    [SerializeField] private float damageMultiplierMin = 2.5f;
    [SerializeField] private float damageMultiplierMax = 0.8f;
    [SerializeField] private float lungeDistanceMultiplierMin = 0.7f;
    [SerializeField] private float lungeDistanceMultiplierMax = 1.8f;
    [SerializeField] private float lungeSpeedMultiplierMin = 0.5f;
    [SerializeField] private float lungeSpeedMultiplierMax = 2.2f;
    [SerializeField] private float scaleAtMinAggression = 2.8f;
    [SerializeField] private float scaleAtMaxAggression = 0.35f;
    [SerializeField] private float hpMultiplierMin = 4.5f;
    [SerializeField] private float hpMultiplierMax = 0.2f;

    [Header("Crowd Spreading")]
    [SerializeField] private float crowdCheckInterval = 0.7f;
    [SerializeField] private float crowdSpreadRadius = 8f;
    [SerializeField] private int maxNearbyAttackersBeforeSpread = 5;
    [SerializeField, Range(0f, 1f)] private float crowdSpreadChance = 0.65f;
    [SerializeField] private float crowdSpreadMinDuration = 4f;
    [SerializeField] private float crowdSpreadMaxDuration = 8f;
    [SerializeField] private float crowdSurroundRadius = 7f;
    [SerializeField] private float crowdAngleJitter = 35f;

    [Header("Anticipation Scaling")]
    [SerializeField] private float anticipationRampDuration = 120f;
    [SerializeField, Range(0f, 1f)] private float interceptChanceMin = 0.05f;
    [SerializeField, Range(0f, 1f)] private float interceptChanceMax = 0.45f;
    [SerializeField] private float interceptLookAheadMin = 0.3f;
    [SerializeField] private float interceptLookAheadMax = 1.2f;
    [SerializeField, Range(0f, 0.9f)] private float maxLungeCooldownReduction = 0.6f;

    [Header("Sprint")]
    [SerializeField] private float sprintSpeedMultiplier = 1.8f;
    [SerializeField] private float maxSprintStamina = 3f;
    [SerializeField] private float staminaRechargeRate = 1f;
    [SerializeField] private float staminaDrainRate = 1f;
    [SerializeField] private float exhaustedSpeedMultiplier = 0.55f;
    [SerializeField] private float staminaRecoverThreshold = 0.5f;
    [SerializeField] private float chargeSprintDistance = 12f;
    [SerializeField, Range(0f, 1f)] private float flankSprintChance = 0.6f;

    [Header("Visibility")]
    [SerializeField] private float visibilityFadeSpeed = 3f;

    [Header("Attack Materialization")]
    [SerializeField] private float materializationRange = 8f;
    [SerializeField] private float materializationDuration = 2f;

    // ── Runtime state ────────────────────────────────────────────────────────

    private Rigidbody rb;
    private Transform playerTarget;
    private Transform playerCamera;
    private GhostClothSetup ghostClothSetup;

    private BehaviourState state = BehaviourState.Inactive;

    private bool wantsToFlyHigh;
    private float personalExtraFlyHeight;
    private bool likesClimbingEnemies;
    private float returnToGroundDistance;
    private float aggressionSpectrum = 0f;

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

    private float flashlightDamage = 0f;
    private bool isInFlashlightBeam;
    private bool wasInFlashlightBeamLastFrame;
    private Vector3 originalScale;
    private float flashlightEffectFactor = 1f;
    private Vector3 flashlightBeamDirection;

    private Vector3 beamEvasionDir;
    private bool hasChosenEvasionDir;

    private float deathTimer;
    private Vector3 fleeDirection;
    private float crowdCheckTimer;
    private float gameTimeSurvived;

    private float effectiveInterceptChance;
    private float effectiveInterceptLookAhead;

    private float sprintStamina;
    private bool isSprinting;
    private bool isExhausted;

    private float _targetVisibility = 0f;
    private float _currentVisibility = 0f;
    private float _materializationProgress = 0f;
    private Collider _mainCollider;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        ghostClothSetup = GetComponent<GhostClothSetup>();

        _mainCollider = GetComponent<Collider>();

        startDelay = Random.Range(0f, 2f);
        formationSlot = formationCounter % 8;
        formationCounter++;

        wantsToFlyHigh = Random.value <= chanceToFlyHigh;
        personalExtraFlyHeight = Random.Range(minExtraFlyHeight, maxExtraFlyHeight);
        likesClimbingEnemies = Random.value <= climbOverEnemyChance;
        returnToGroundDistance = Random.Range(2.5f, 7f);

        aggressionSpectrum = 0f;

        flankSide = Random.value < 0.5f ? 1f : -1f;
        flankAngle = formationSlot * (360f / 8f);
        flankSideTimer = flankSideFlipInterval * Random.Range(0.5f, 1.5f);

        lungeCooldownTimer.ResetRandom(0f, 10f);
        currentTargetHeight = Random.Range(minWanderHeight, maxWanderHeight);
        heightTimer = Random.Range(0f, heightChangeInterval);
        originalScale = transform.localScale;

        crowdCheckTimer = crowdCheckInterval * Random.Range(0.5f, 1.5f);
        sprintStamina = maxSprintStamina;

        effectiveInterceptChance = interceptChanceMin;
        effectiveInterceptLookAhead = interceptLookAheadMin;

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
        UpdateMaterialization(dt);
        UpdateVisibility();

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
        UpdateSprint(dt);
        ApplyMovement(dt);

        wasInFlashlightBeamLastFrame = isInFlashlightBeam;
        isInFlashlightBeam = false;
        flashlightEffectFactor = 0f;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void SetTarget(Transform target) => playerTarget = target;

    public void SetSurvivedTime(float survivedTime)
    {
        gameTimeSurvived = survivedTime;

        float spreadT = aggressionSpreadDuration > 0f
            ? Mathf.Clamp01(survivedTime / aggressionSpreadDuration)
            : 1f;

        float targetSpectrum = Mathf.Lerp(-1f, 1f, (float)formationSlot / 7f)
            + Random.Range(-0.15f, 0.15f);
        targetSpectrum = Mathf.Clamp(targetSpectrum, -1f, 1f);
        aggressionSpectrum = Mathf.Lerp(0f, targetSpectrum, spreadT);

        float antT = anticipationRampDuration > 0f
            ? Mathf.Clamp01(survivedTime / anticipationRampDuration)
            : 1f;
        effectiveInterceptChance = Mathf.Lerp(interceptChanceMin, interceptChanceMax, antT);
        effectiveInterceptLookAhead = Mathf.Lerp(interceptLookAheadMin, interceptLookAheadMax, antT);

        ApplyAggressionStats(antT);
    }

    private void ApplyAggressionStats(float antT = 0f)
    {
        float t = (aggressionSpectrum + 1f) * 0.5f;

        moveSpeed *= Mathf.Lerp(speedMultiplierMin, speedMultiplierMax, t);
        damagePercentage *= Mathf.Lerp(damageMultiplierMin, damageMultiplierMax, t);
        lungeTriggerDistance *= Mathf.Lerp(lungeDistanceMultiplierMin, lungeDistanceMultiplierMax, t);
        lungeSpeed *= Mathf.Lerp(lungeSpeedMultiplierMin, lungeSpeedMultiplierMax, t);

        flashlightKillTime *= Mathf.Lerp(hpMultiplierMin, hpMultiplierMax, t);
        flashlightKillTime = Mathf.Max(0.3f, flashlightKillTime);

        float scaleFactor = Mathf.Lerp(scaleAtMinAggression, scaleAtMaxAggression, t);
        originalScale = transform.localScale * scaleFactor;
        transform.localScale = originalScale;

        float cooldownReduction = maxLungeCooldownReduction * antT;
        lungeCooldownTimer.Reset(lungeCooldown * (1f - cooldownReduction));

        // Vertel het cloth systeem dat de schaal eenmalig is veranderd.
        ghostClothSetup?.NotifyScaleChanged();
    }

    public void TakeRecoil(Vector3 hitDirection)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        recoilDir = new Vector3(-hitDirection.x, 0f, -hitDirection.z).normalized;
        recoilTimer = recoilDuration;
        state = BehaviourState.Recoil;
    }

    public void SetTargetVisibility(float visibility)
    {
        _targetVisibility = visibility;
        Debug.Log($"[Ghost] SetTargetVisibility aangeroepen: {visibility}");
    }

    // ── Flashlight ───────────────────────────────────────────────────────────

    public void ReceiveFlashlightHit(float effectFactor = 1f, Vector3 beamDirection = default)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        isInFlashlightBeam = true;
        flashlightEffectFactor = Mathf.Max(flashlightEffectFactor, effectFactor);

        if (beamDirection != Vector3.zero)
            flashlightBeamDirection = beamDirection;

        if (!wasInFlashlightBeamLastFrame)
        {
            isSprinting = false;

            if (state == BehaviourState.Lunge)
                lungeMoveVelocity = Vector3.zero;

            EnterWeakened();
        }
    }

    private void UpdateFlashlightExposure(float dt)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        if (isInFlashlightBeam)
        {
            flashlightDamage += dt / flashlightKillTime;
            flashlightDamage = Mathf.Clamp01(flashlightDamage);

            if (flashlightDamage >= 1f)
                BeginDying();
        }
        else if (state == BehaviourState.Weakened && flashlightDamage > 0f)
        {
            float spectrumHealMultiplier = Mathf.Lerp(3.5f, 0.5f, (aggressionSpectrum + 1f) * 0.5f);
            flashlightDamage -= dt / (healTime * partialHealTimeMultiplier * spectrumHealMultiplier);
            flashlightDamage = Mathf.Max(0f, flashlightDamage);

            if (flashlightDamage <= 0f)
            {
                float dist = playerTarget != null
                    ? Vector3.Distance(transform.position, playerTarget.position)
                    : 0f;
                TransitionToAttack(dist);
            }
        }

        ApplyDamageScale();
    }

    private void ApplyDamageScale()
    {
        if (state == BehaviourState.Dying)
            return;

        float visualDamage = isInFlashlightBeam
            ? Mathf.Max(flashlightDamage, 0.15f)
            : flashlightDamage;

        float scaleFraction = Mathf.Lerp(1f, weakenedMinScaleFraction, visualDamage);
        Vector3 targetScale = originalScale * scaleFraction;
        Vector3 prevScale = transform.localScale;
        transform.localScale = Vector3.MoveTowards(
            transform.localScale, targetScale, scaleChangeSpeed * Time.fixedDeltaTime);

        // Vertel het cloth systeem dat de schaal is veranderd zodat het jitter kan voorkomen.
        if (transform.localScale != prevScale)
            ghostClothSetup?.NotifyScaleChanged();
    }

    private void EnterWeakened()
    {
        if (state == BehaviourState.Dying) return;
        state = BehaviourState.Weakened;
    }

    private void BeginDying()
    {
        state = BehaviourState.Dying;
        deathTimer = deathRiseDuration;
        rb.linearVelocity = Vector3.zero;
    }

    private void UpdateMaterialization(float dt)
    {
        bool shouldMaterialize = playerTarget != null
            && (state == BehaviourState.Chase || state == BehaviourState.Lunge)
            && Vector3.Distance(transform.position, playerTarget.position) <= materializationRange;

        float direction = shouldMaterialize ? 1f : -1f;
        _materializationProgress = Mathf.Clamp01(_materializationProgress + direction * dt / materializationDuration);

        if (_mainCollider != null)
            _mainCollider.enabled = _materializationProgress > 0f;
    }

    private void UpdateVisibility()
    {
        float effectiveTarget = Mathf.Max(_targetVisibility, flashlightDamage, _materializationProgress, 0.05f);
        _currentVisibility = Mathf.Lerp(_currentVisibility, effectiveTarget, Time.fixedDeltaTime * visibilityFadeSpeed);
        ghostClothSetup?.SetVisibility(_currentVisibility);
    }

    // ── Sprint / stamina ─────────────────────────────────────────────────────

    private void UpdateSprint(float dt)
    {
        if (state == BehaviourState.Inactive ||
            state == BehaviourState.Dying ||
            state == BehaviourState.Fleeing ||
            state == BehaviourState.Weakened ||
            state == BehaviourState.Recoil ||
            state == BehaviourState.Lunge)
        {
            isSprinting = false;
            RechargeStamina(dt);
            return;
        }

        bool wantsToSprint = false;

        if (!isExhausted)
        {
            if ((state == BehaviourState.Chase || state == BehaviourState.Intercept) && playerTarget != null)
                wantsToSprint = Vector3.Distance(transform.position, playerTarget.position) <= chargeSprintDistance;
            else if (state == BehaviourState.Flank)
                wantsToSprint = isSprinting;
        }

        if (wantsToSprint && sprintStamina > 0f)
        {
            isSprinting = true;
            sprintStamina -= staminaDrainRate * dt;

            if (sprintStamina <= 0f)
            {
                sprintStamina = 0f;
                isExhausted = true;
                isSprinting = false;
            }
        }
        else
        {
            isSprinting = false;
            RechargeStamina(dt);
        }
    }

    private void RechargeStamina(float dt)
    {
        if (sprintStamina < maxSprintStamina)
        {
            sprintStamina = Mathf.Min(sprintStamina + staminaRechargeRate * dt, maxSprintStamina);
            if (isExhausted && sprintStamina >= staminaRecoverThreshold)
                isExhausted = false;
        }
    }

    private void TryBeginFlankSprint()
    {
        if (!isExhausted && sprintStamina >= staminaRecoverThreshold && Random.value <= flankSprintChance)
            isSprinting = true;
    }

    // ── State machine ────────────────────────────────────────────────────────

    private void UpdateState(float dt)
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (state == BehaviourState.Dying)
        {
            deathTimer -= dt;
            rb.linearVelocity = new Vector3(0f, deathLaunchSpeed, 0f);
            transform.rotation *= Quaternion.Euler(0f, deathSpinSpeed * dt, 0f);
            if (deathTimer <= 0f) Destroy(gameObject);
            return;
        }

        if (state == BehaviourState.Fleeing)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f) BeginDying();
            return;
        }

        if (state == BehaviourState.Weakened)
            return;

        if (state == BehaviourState.Recoil)
        {
            recoilTimer -= dt;
            if (recoilTimer <= 0f)
            {
                if (isInFlashlightBeam || flashlightDamage > 0f)
                    EnterWeakened();
                else
                    TransitionToAttack(distToPlayer);
            }
            return;
        }

        if (state == BehaviourState.Inactive)
        {
            startDelay -= dt;
            if (startDelay <= 0f) TransitionToAttack(distToPlayer);
            return;
        }

        if (state == BehaviourState.Lunge)
        {
            UpdateLunge(dt, distToPlayer);
            return;
        }

        crowdCheckTimer -= dt;
        if (crowdCheckTimer <= 0f)
        {
            crowdCheckTimer = crowdCheckInterval;
            CheckCrowdSpread(distToPlayer);
        }

        float aggrT = (aggressionSpectrum + 1f) * 0.5f;
        float effectiveLungeTrigger = lungeTriggerDistance * Mathf.Lerp(0.6f, 1.4f, aggrT);
        float effectiveLungeChance = lungeChance * Mathf.Lerp(0.4f, 1.6f, aggrT);

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
                if (stateTimer <= 0f) TransitionToAttack(distToPlayer);
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

    private void CheckCrowdSpread(float distToPlayer)
    {
        if (state != BehaviourState.Chase) return;

        Collider[] nearby = Physics.OverlapSphere(
            playerTarget.position, crowdSpreadRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        if (nearby.Length < maxNearbyAttackersBeforeSpread) return;
        if (Random.value > crowdSpreadChance) return;

        float baseAngle = formationSlot * (360f / 8f);
        flankAngle = baseAngle + Random.Range(-crowdAngleJitter, crowdAngleJitter);

        state = BehaviourState.Flank;
        stateTimer = Random.Range(crowdSpreadMinDuration, crowdSpreadMaxDuration);
        TryBeginFlankSprint();
    }

    private void TransitionToAttack(float distToPlayer)
    {
        bool canIntercept = distToPlayer >= minInterceptDistance && distToPlayer <= interceptDistance;
        bool choosesIntercept = canIntercept && Random.value <= effectiveInterceptChance;

        if (choosesIntercept)
        {
            state = BehaviourState.Intercept;
            stateTimer = Random.Range(interceptMinDuration, interceptMaxDuration);
        }
        else if (Random.value > (aggressionSpectrum + 1f) * 0.5f && Random.value <= flankChance)
        {
            state = BehaviourState.Flank;
            stateTimer = flankDuration + Random.Range(-3f, 3f);
            TryBeginFlankSprint();
        }
        else
        {
            state = BehaviourState.Chase;
            stateTimer = Random.Range(3f, 8f);
        }
    }

    // ── Lunge ────────────────────────────────────────────────────────────────

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

    // ── Flank ────────────────────────────────────────────────────────────────

    private void UpdateFlankAngle(float dt)
    {
        bool playerLooking = VisibilityChecker.IsTransformVisibleToCamera(
            transform, playerCamera, visibilityDotThreshold);

        flankSideTimer -= playerLooking ? dt * 2f : dt;
        if (flankSideTimer <= 0f)
        {
            flankSide = -flankSide;
            flankSideTimer = flankSideFlipInterval * Random.Range(0.6f, 1.4f);
        }

        float orbitDelta = flankOrbitSpeed * dt * flankSide * Mathf.Rad2Deg;
        if (playerLooking) orbitDelta *= 0.15f;
        flankAngle += orbitDelta;
    }

    // ── Movement ─────────────────────────────────────────────────────────────

    private void ApplyMovement(float dt)
    {
        if (state == BehaviourState.Inactive || state == BehaviourState.Dying)
            return;

        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float yVelocity = GetVerticalVelocity();

        if (state == BehaviourState.Recoil)
        {
            rb.linearVelocity = new Vector3(recoilDir.x * recoilForce, yVelocity, recoilDir.z * recoilForce);
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
        if (_materializationProgress > 0f && _materializationProgress < 1f)
            targetSpeed *= 0.5f;
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

        Vector3 result = primary
            + sep * separationStrength
            + obstacle * avoidanceStrength
            + boundary * boundaryStrength;

        if (isInFlashlightBeam
            && state != BehaviourState.Weakened
            && state != BehaviourState.Recoil
            && Random.value <= beamEvasionChance)
        {
            result += GetBeamEvasionVector(toPlayerFlat) * beamEvasionStrength;
        }

        return result;
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
            + playerVel * timeToReach * effectiveInterceptLookAhead
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

    private Vector3 GetWeakenedDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        Vector3 retreat = -toPlayerFlat;
        Vector3 evasion = GetBeamEvasionVector(toPlayerFlat);

        float evasionBlend = Mathf.Lerp(0.1f, 1.2f, (aggressionSpectrum + 1f) * 0.5f);
        return (retreat + evasion * evasionBlend).normalized;
    }

    private Vector3 GetBeamEvasionVector(Vector3 toPlayerFlat)
    {
        if (!isInFlashlightBeam)
        {
            hasChosenEvasionDir = false;
            return Vector3.zero;
        }

        if (!hasChosenEvasionDir || beamEvasionDir == Vector3.zero)
        {
            Vector3 perp = Vector3.Cross(toPlayerFlat, Vector3.up);
            beamEvasionDir = (Random.value < 0.5f ? perp : -perp).normalized;
            hasChosenEvasionDir = true;
        }

        return beamEvasionDir;
    }

    // ── Steering helpers ─────────────────────────────────────────────────────

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
            (Vector3.back    + Vector3.right).normalized,
            (Vector3.back    + Vector3.left).normalized,
        };

        foreach (Vector3 dir in dirs)
        {
            Vector3 origin = transform.position + dir * boundaryLookAhead + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
                push -= dir;
        }
        return push.sqrMagnitude > 0.01f ? push.normalized : Vector3.zero;
    }

    // ── Speed & height ───────────────────────────────────────────────────────

    private float GetTargetSpeed(float distToPlayer)
    {
        if (state == BehaviourState.Weakened)
        {
            float speedFraction = 1f - (flashlightEffectFactor * 0.9f);
            float baseWeakenedSpeed = weakenedSpeed * Mathf.Max(speedFraction, 0.1f);

            float spectrumMultiplier = Mathf.Lerp(0.4f, 2.0f, (aggressionSpectrum + 1f) * 0.5f);
            return baseWeakenedSpeed * spectrumMultiplier;
        }
        if (state == BehaviourState.Fleeing) return fleeSpeed;

        float baseSpeed = moveSpeed * chaseSpeedMultiplier;
        float speed = baseSpeed * GetDistanceBoost(distToPlayer) * GetFatigueMultiplier();

        if (isSprinting) speed *= sprintSpeedMultiplier;
        else if (isExhausted) speed *= exhaustedSpeedMultiplier;

        return speed;
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
        if (state == BehaviourState.Dying) return 0f;

        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float targetY = GetTargetFlyingHeight(distToPlayer);
        float diff = targetY - transform.position.y;

        if (state == BehaviourState.Weakened || isInFlashlightBeam)
        {
            float fallSpeed = diff < 0f ? weakenedFallSpeed : verticalSpeed * 0.3f;
            return Mathf.Clamp(diff * fallSpeed, -weakenedFallSpeed, verticalSpeed * 0.3f);
        }

        float speed = IsObstacleAhead() ? obstacleLiftSpeed : verticalSpeed;
        return Mathf.Clamp(diff * speed, -verticalSpeed, speed);
    }

    private float GetTargetFlyingHeight(float distToPlayer)
    {
        float surfaceY = GetSurfaceY();
        float restHeight = surfaceY + preferredFloatHeight;
        float maxHeight = surfaceY + maxFloatHeight;

        if (state == BehaviourState.Weakened || isInFlashlightBeam)
            return Mathf.Lerp(restHeight, surfaceY + weakenedMinFloatHeight, flashlightDamage);

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

        float clearanceHeight = GetObstacleClearHeight();
        if (clearanceHeight > desiredHeight) desiredHeight = clearanceHeight;

        return Mathf.Clamp(desiredHeight, restHeight, maxHeight);
    }

    private float GetObstacleClearHeight()
    {
        Vector3 moveDir = new Vector3(smoothedVelocity.x, 0f, smoothedVelocity.z);
        if (moveDir.sqrMagnitude < 0.01f)
            moveDir = new Vector3(transform.forward.x, 0f, transform.forward.z);
        moveDir.Normalize();

        Vector3 origin = transform.position + Vector3.up * 0.5f;

        if (!Physics.SphereCast(origin, obstacleScanRadius, moveDir, out RaycastHit hit,
                obstacleScanDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
            return float.MinValue;

        return hit.collider.bounds.max.y + obstacleOvershootHeight;
    }

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

        float groundY = Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit,
            heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore)
            ? groundHit.point.y : 0f;

        float obstacleY = Physics.Raycast(origin, Vector3.down, out RaycastHit obstacleHit,
            heightRaycastDistance, obstacleLayers, QueryTriggerInteraction.Ignore)
            ? obstacleHit.point.y : float.MinValue;

        return Mathf.Max(groundY, obstacleY);
    }

    // ── Utilities ────────────────────────────────────────────────────────────

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ── Combat ───────────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        if (_materializationProgress < 0.75f) return;

        HealthController health = collision.gameObject.GetComponentInParent<HealthController>();
        if (health == null || !health.CompareTag("Player")) return;

        hasHit = true;

        float sizeFraction = originalScale.x > 0f
            ? transform.localScale.x / originalScale.x
            : 1f;

        health.TakeDamage(health.MaxHealth * damagePercentage * sizeFraction);

        PlayerController pc = health.GetComponent<PlayerController>();
        if (pc != null)
        {
            Vector3 dir = health.transform.position - transform.position;
            dir.y = 0f;
            pc.ApplyKnockback(dir, knockbackForce * sizeFraction, upwardKnockbackForce * sizeFraction);
        }

        Destroy(gameObject);
    }
}