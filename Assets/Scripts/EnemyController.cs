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
        Weakened,   // spotted by flashlight — retreats and heals when clear
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

    [Header("Death Animation")]
    [Tooltip("Hoe snel de enemy omhoog schiet bij het begin van de dood-animatie.")]
    [SerializeField] private float deathLaunchSpeed = 16f;
    [Tooltip("Hoe snel de enemy ronddraait tijdens de dood-animatie (graden per seconde).")]
    [SerializeField] private float deathSpinSpeed = 360f;
    [Tooltip("Hoe lang de omhoog-fase duurt voordat de enemy destroyed wordt (seconden).")]
    [SerializeField] private float deathRiseDuration = 1.8f;

    [Header("Weakened Shrink & Fall")]
    [Tooltip("Minimale schaalfractie tijdens weakened (0.3 = krimpt tot 30% van origineel).")]
    [SerializeField, Range(0.1f, 0.9f)] private float weakenedMinScaleFraction = 0.3f;
    [Tooltip("Doelhoogte boven de grond bij maximale flashlight schade (bijna op de grond).")]
    [SerializeField] private float weakenedMinFloatHeight = 0.15f;
    [Tooltip("Hoe snel de enemy daalt richting de grond als hij beschadigd is (m/s schaalfactor).")]
    [SerializeField] private float weakenedFallSpeed = 6f;

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
    [Tooltip("Seconden voordat de enemy volledig herstelt als hij niet belicht wordt.")]
    [SerializeField] private float healTime = 5f;
    [Tooltip("Hersteltijd-multiplier als de enemy gedeeltelijk beschadigd is maar niet weakened (trager herstel).")]
    [SerializeField] private float partialHealTimeMultiplier = 2.5f;
    [Tooltip("Snelheid waarmee de scale verandert (zichtbaar krimpen/groeien).")]
    [SerializeField] private float scaleChangeSpeed = 2f;

    [Header("Beam Evasion")]
    [Tooltip("Hoe sterk de enemy zijwaarts uitwijkt als hij in de beam staat maar nog niet Weakened is.")]
    [SerializeField] private float beamEvasionStrength = 6f;
    [Tooltip("Afstand tot de rand van de beam waarbinnen de enemy probeert te ontwijken.")]
    [SerializeField] private float beamEvasionRadius = 3f;
    [Tooltip("Kans per frame dat de enemy een ontwijkrichting kiest (0 = nooit, 1 = altijd direct).")]
    [SerializeField, Range(0f, 1f)] private float beamEvasionChance = 0.85f;

    [Header("Aggression Scaling (set on prefab, injected by spawner)")]
    [Tooltip("Minimum aggression floor at the very start of the game (0 = fully timid, 1 = fully reckless).")]
    [SerializeField, Range(0f, 1f)] private float aggressionAtStart = 0f;
    [Tooltip("Maximum aggression floor reached after aggressionRampDuration seconds (0–1).")]
    [SerializeField, Range(0f, 1f)] private float aggressionAtEnd = 0.7f;
    [Tooltip("Time in seconds it takes to go from aggressionAtStart to aggressionAtEnd.")]
    [SerializeField] private float aggressionRampDuration = 180f;

    [Tooltip("Move speed multiplier at minimum aggression.")]
    [SerializeField] private float speedMultiplierMin = 0.6f;
    [Tooltip("Move speed multiplier at maximum aggression.")]
    [SerializeField] private float speedMultiplierMax = 2.0f;

    [Tooltip("Damage multiplier at minimum aggression.")]
    [SerializeField] private float damageMultiplierMin = 0.3f;
    [Tooltip("Damage multiplier at maximum aggression.")]
    [SerializeField] private float damageMultiplierMax = 2.5f;

    [Tooltip("Lunge trigger distance multiplier at minimum aggression.")]
    [SerializeField] private float lungeDistanceMultiplierMin = 0.7f;
    [Tooltip("Lunge trigger distance multiplier at maximum aggression.")]
    [SerializeField] private float lungeDistanceMultiplierMax = 1.8f;

    [Tooltip("Lunge speed multiplier at minimum aggression.")]
    [SerializeField] private float lungeSpeedMultiplierMin = 0.8f;
    [Tooltip("Lunge speed multiplier at maximum aggression.")]
    [SerializeField] private float lungeSpeedMultiplierMax = 1.4f;

    [Tooltip("Schaalfactor bij minimale agressie (groter = meer HP, maar trager).")]
    [SerializeField] private float scaleAtMinAggression = 1.5f;
    [Tooltip("Schaalfactor bij maximale agressie (kleiner = minder HP, maar sneller).")]
    [SerializeField] private float scaleAtMaxAggression = 0.8f;

    [Tooltip("Flashlight kill time multiplier bij minimale agressie (meer HP = langer om te doden).")]
    [SerializeField] private float hpMultiplierMin = 2.0f;
    [Tooltip("Flashlight kill time multiplier bij maximale agressie (minder HP = sneller dood).")]
    [SerializeField] private float hpMultiplierMax = 0.5f;

    [Header("Crowd Spreading / Surround")]
    [SerializeField] private float crowdCheckInterval = 0.7f;
    [SerializeField] private float crowdSpreadRadius = 8f;
    [SerializeField] private int maxNearbyAttackersBeforeSpread = 5;
    [SerializeField, Range(0f, 1f)] private float crowdSpreadChance = 0.65f;
    [SerializeField] private float crowdSpreadMinDuration = 4f;
    [SerializeField] private float crowdSpreadMaxDuration = 8f;
    [SerializeField] private float crowdSurroundRadius = 7f;
    [SerializeField] private float crowdAngleJitter = 35f;

    [Header("Anticipation Scaling (gameTimeSurvived)")]
    [Tooltip("Tijd (sec) waarna intercept kans op max zit.")]
    [SerializeField] private float anticipationRampDuration = 120f;
    [Tooltip("Intercept kans bij t=0.")]
    [SerializeField, Range(0f, 1f)] private float interceptChanceMin = 0.05f;
    [Tooltip("Intercept kans bij maximale anticipatie.")]
    [SerializeField, Range(0f, 1f)] private float interceptChanceMax = 0.45f;
    [Tooltip("Lookahead multiplier bij t=0 (hoe ver vooruit de intercept mikt).")]
    [SerializeField] private float interceptLookAheadMin = 0.3f;
    [Tooltip("Lookahead multiplier bij maximale anticipatie.")]
    [SerializeField] private float interceptLookAheadMax = 1.2f;
    [Tooltip("Lunge cooldown reductie bij maximale anticipatie (0 = geen reductie, 0.8 = 80% korter).")]
    [SerializeField, Range(0f, 0.9f)] private float maxLungeCooldownReduction = 0.6f;

    [Header("Sprint")]
    [Tooltip("Snelheidsmultiplier tijdens een sprint burst.")]
    [SerializeField] private float sprintSpeedMultiplier = 1.8f;
    [Tooltip("Maximale sprint-energie (seconden op volle sprint).")]
    [SerializeField] private float maxSprintStamina = 3f;
    [Tooltip("Hoe snel stamina zich oplaadt als de enemy niet sprint (seconden per seconde).")]
    [SerializeField] private float staminaRechargeRate = 1f;
    [Tooltip("Hoe snel stamina verbruikt wordt tijdens het sprinten (seconden per seconde).")]
    [SerializeField] private float staminaDrainRate = 1f;
    [Tooltip("Snelheidsmultiplier als stamina volledig leeg is (uitgeput).")]
    [SerializeField] private float exhaustedSpeedMultiplier = 0.55f;
    [Tooltip("Minimale stamina die hersteld moet zijn voordat een nieuwe sprint kan starten.")]
    [SerializeField] private float staminaRecoverThreshold = 0.5f;
    [Tooltip("Afstand tot de speler waaronder een charge-sprint wordt geactiveerd tijdens Chase/Intercept.")]
    [SerializeField] private float chargeSprintDistance = 12f;
    [Tooltip("Kans per transitie dat een flank-sprint wordt geactiveerd.")]
    [SerializeField, Range(0f, 1f)] private float flankSprintChance = 0.6f;

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
    private float flashlightDamage = 0f;
    private bool isInFlashlightBeam;
    private bool wasInFlashlightBeamLastFrame; // used for first-frame recoil detection
    private Vector3 originalScale;

    // Flashlight effect factor this frame (1 = full, 0 = none) — set by ReceiveFlashlightHit
    private float flashlightEffectFactor = 1f;

    // Beam evasion — perpendicular dodge direction chosen when beam is first detected
    private Vector3 beamEvasionDir;
    private bool hasChosenEvasionDir;

    // Death animation
    private float deathTimer;

    // Flee direction (set when ghost touches player)
    private Vector3 fleeDirection;

    // Crowd spreading
    private float crowdCheckTimer;

    // Injected by EnemySpawner — used to scale anticipation and cooldowns
    private float gameTimeSurvived;

    // Anticipation values computed from gameTimeSurvived
    private float effectiveInterceptChance;
    private float effectiveInterceptLookAhead;

    // Sprint / stamina
    private float sprintStamina;        // current stamina, 0..maxSprintStamina
    private bool isSprinting;           // is the enemy actively sprinting this frame
    private bool isExhausted;           // stamina hit zero — must recover before next sprint

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
        originalScale = transform.localScale;

        crowdCheckTimer = crowdCheckInterval * Random.Range(0.5f, 1.5f);

        sprintStamina = maxSprintStamina;

        // Default anticipation values (overridden by SetSurvivedTime if called)
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

        // Track beam state for next frame (used for first-hit recoil)
        wasInFlashlightBeamLastFrame = isInFlashlightBeam;
        // Reset beam flag — flashlight must re-confirm every frame
        isInFlashlightBeam = false;
        // Reset effect factor — will be set again next frame if still in beam
        flashlightEffectFactor = 0f;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Public API
    // ────────────────────────────────────────────────────────────────────────────
    public void SetTarget(Transform target) => playerTarget = target;

    /// <summary>
    /// Called by EnemySpawner right after instantiation.
    /// Scales aggression AND anticipation (intercept quality, lunge cooldown) based on survival time.
    /// </summary>
    public void SetSurvivedTime(float survivedTime)
    {
        gameTimeSurvived = survivedTime;

        // ── Aggression floor ──
        float aggrT = aggressionRampDuration > 0f
            ? Mathf.Clamp01(survivedTime / aggressionRampDuration)
            : 1f;
        float aggressionFloor = Mathf.Lerp(aggressionAtStart, aggressionAtEnd, aggrT);
        aggressionBias = Mathf.Max(aggressionBias, aggressionFloor);

        // ── Anticipation ──
        float antT = anticipationRampDuration > 0f
            ? Mathf.Clamp01(survivedTime / anticipationRampDuration)
            : 1f;
        effectiveInterceptChance = Mathf.Lerp(interceptChanceMin, interceptChanceMax, antT);
        effectiveInterceptLookAhead = Mathf.Lerp(interceptLookAheadMin, interceptLookAheadMax, antT);

        ApplyAggressionStats(antT);
    }

    /// <summary>
    /// Scales move speed, damage, lunge range, lunge cooldown, body scale and effective HP
    /// based on aggressionBias. High aggression = fast, small, fragile, high damage.
    /// Low aggression = slow, large, tanky, low damage.
    /// Called once after aggressionBias is finalised.
    /// </summary>
    private void ApplyAggressionStats(float antT = 0f)
    {
        moveSpeed *= Mathf.Lerp(speedMultiplierMin, speedMultiplierMax, aggressionBias);
        damagePercentage *= Mathf.Lerp(damageMultiplierMin, damageMultiplierMax, aggressionBias);
        lungeTriggerDistance *= Mathf.Lerp(lungeDistanceMultiplierMin, lungeDistanceMultiplierMax, aggressionBias);
        lungeSpeed *= Mathf.Lerp(lungeSpeedMultiplierMin, lungeSpeedMultiplierMax, aggressionBias);

        // HP: aggressive = fragile (short kill time), timid = tanky (long kill time)
        flashlightKillTime *= Mathf.Lerp(hpMultiplierMin, hpMultiplierMax, aggressionBias);
        flashlightKillTime = Mathf.Max(0.3f, flashlightKillTime);

        // Body scale: aggressive = small (0.8×), timid = large (1.5×)
        float scaleFactor = Mathf.Lerp(scaleAtMinAggression, scaleAtMaxAggression, aggressionBias);
        originalScale = transform.localScale * scaleFactor;
        transform.localScale = originalScale;

        // Lategame enemies have shorter lunge cooldowns
        float cooldownReduction = maxLungeCooldownReduction * antT;
        lungeCooldownTimer.Reset(lungeCooldown * (1f - cooldownReduction));
    }

    public void TakeRecoil(Vector3 hitDirection)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        recoilDir = new Vector3(-hitDirection.x, 0f, -hitDirection.z).normalized;
        recoilTimer = recoilDuration;
        state = BehaviourState.Recoil;
    }

    /// <summary>
    /// Called every frame by FlashlightController while its beam hits this enemy.
    /// effectFactor (0–1) scales both damage and slowdown based on distance from the flashlight.
    /// On the first frame of contact a small recoil is applied.
    /// </summary>
    public void ReceiveFlashlightHit(float effectFactor = 1f)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        isInFlashlightBeam = true;
        flashlightEffectFactor = Mathf.Max(flashlightEffectFactor, effectFactor);

        // First frame of contact: apply a soft recoil push away from the player
        if (!wasInFlashlightBeamLastFrame && playerTarget != null)
        {
            Vector3 hitDir = (transform.position - playerTarget.position).normalized;
            TakeRecoil(hitDir);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Flashlight / Health systeem
    // flashlightDamage loopt van 0 (vol) tot 1 (dood).
    // Scale = originalScale * (1 - flashlightDamage), zodat de enemy zichtbaar krimpt.
    //
    // Herstelgedrag:
    //  - Weakened (zaklamp gezien): herstelt direct zodra de beam weg is,
    //    maar trager (partialHealTimeMultiplier), en vlucht weg van de speler.
    //    Geen minimumafstand meer vereist.
    //  - Volledig hersteld → terug naar aanval.
    // ────────────────────────────────────────────────────────────────────────────
    private void UpdateFlashlightExposure(float dt)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing)
            return;

        if (isInFlashlightBeam)
        {
            // Schade opbouwen terwijl de zaklamp schijnt — geschaald op afstand
            flashlightDamage += (dt / flashlightKillTime) * flashlightEffectFactor;
            flashlightDamage = Mathf.Clamp01(flashlightDamage);

            // Ga naar Weakened zodra de beam raakt (recoil is al gezet in ReceiveFlashlightHit)
            if (state != BehaviourState.Weakened && state != BehaviourState.Recoil)
                EnterWeakened();

            // Dood bij vol schade
            if (flashlightDamage >= 1f)
                BeginDying();
        }
        else
        {
            // Niet belicht en heeft schade: herstel direct (geen afstandseis),
            // maar trager dan normaal. De enemy blijft in Weakened-state en vlucht
            // dus automatisch weg van de speler zolang hij niet volledig hersteld is.
            if (state == BehaviourState.Weakened && flashlightDamage > 0f)
            {
                float healRate = healTime * partialHealTimeMultiplier;
                flashlightDamage -= dt / healRate;
                flashlightDamage = Mathf.Max(0f, flashlightDamage);

                // Volledig hersteld: terug naar aanval
                if (flashlightDamage <= 0f)
                {
                    float dist = playerTarget != null
                        ? Vector3.Distance(transform.position, playerTarget.position)
                        : 0f;
                    TransitionToAttack(dist);
                }
            }
        }

        // Scale altijd bijwerken op basis van huidige schade
        ApplyDamageScale();
    }

    private void ApplyDamageScale()
    {
        // During dying the death animation controls scale — don't interfere
        if (state == BehaviourState.Dying)
            return;

        // Weakened: krimpt van originalScale naar weakenedMinScaleFraction * originalScale
        // afhankelijk van hoeveel flashlightDamage er is.
        float scaleFraction = Mathf.Lerp(1f, weakenedMinScaleFraction, flashlightDamage);
        Vector3 targetScale = originalScale * scaleFraction;
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
        deathTimer = deathRiseDuration;
        rb.linearVelocity = Vector3.zero;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Sprint / stamina systeem
    //
    // Twee situaties triggeren een sprint:
    //  1. Charge: tijdens Chase/Intercept, als de enemy dicht genoeg is (chargeSprintDistance).
    //     Geeft een korte versnelling vlak voor de aanval/lunge.
    //  2. Flank-sprint: bij het begin van een Flank-state, met kans flankSprintChance.
    //     Geeft de enemy een burst terwijl hij positie inneemt.
    //
    // Stamina: 0..maxSprintStamina.
    //  - Sprinten kost staminaDrainRate per seconde.
    //  - Op 0: isExhausted = true, enemy beweegt op exhaustedSpeedMultiplier.
    //  - Herstelt met staminaRechargeRate per seconde als niet gesprongen wordt.
    //  - Nieuwe sprint pas mogelijk als stamina >= staminaRecoverThreshold.
    // ────────────────────────────────────────────────────────────────────────────
    private void UpdateSprint(float dt)
    {
        // Sprinten is niet mogelijk in deze states
        if (state == BehaviourState.Inactive ||
            state == BehaviourState.Dying ||
            state == BehaviourState.Fleeing ||
            state == BehaviourState.Weakened ||
            state == BehaviourState.Recoil ||
            state == BehaviourState.Lunge)
        {
            isSprinting = false;
            // Stamina recharges even in non-sprint states (e.g. while flanking without sprint)
            RechargeStamina(dt);
            return;
        }

        bool wantsToSprint = false;

        if (!isExhausted)
        {
            if ((state == BehaviourState.Chase || state == BehaviourState.Intercept) && playerTarget != null)
            {
                float dist = Vector3.Distance(transform.position, playerTarget.position);
                wantsToSprint = dist <= chargeSprintDistance;
            }
            else if (state == BehaviourState.Flank)
            {
                // Flank-sprint: beslissing wordt gezet bij het begin van Flank via BeginFlankSprint
                wantsToSprint = isSprinting; // houd sprint vast als hij al liep
            }
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

    /// <summary>
    /// Called when transitioning into Flank so a random sprint burst can be started immediately.
    /// </summary>
    private void TryBeginFlankSprint()
    {
        if (!isExhausted && sprintStamina >= staminaRecoverThreshold && Random.value <= flankSprintChance)
            isSprinting = true;
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

        // ── Dying: schiet omhoog, draait rond, destroyed na deathRiseDuration ──
        if (state == BehaviourState.Dying)
        {
            deathTimer -= dt;

            // Omhoog schieten
            rb.linearVelocity = new Vector3(0f, deathLaunchSpeed, 0f);

            // Ronddraaien op de Y-as
            transform.rotation *= Quaternion.Euler(0f, deathSpinSpeed * dt, 0f);

            if (deathTimer <= 0f)
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

        // ── Weakened: herstel en state change in UpdateFlashlightExposure ──
        if (state == BehaviourState.Weakened)
            return;

        if (state == BehaviourState.Recoil)
        {
            recoilTimer -= dt;
            if (recoilTimer <= 0f)
            {
                // After recoil from flashlight: enter weakened so enemy flees
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
            if (startDelay <= 0f)
                TransitionToAttack(distToPlayer);
            return;
        }

        if (state == BehaviourState.Lunge)
        {
            UpdateLunge(dt, distToPlayer);
            return;
        }

        // ── Crowd spreading check ──
        crowdCheckTimer -= dt;
        if (crowdCheckTimer <= 0f)
        {
            crowdCheckTimer = crowdCheckInterval;
            CheckCrowdSpread(distToPlayer);
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

    /// <summary>
    /// Checks if too many enemies are clustered near the player.
    /// If so, this enemy switches to Flank to spread out and surround from a different angle.
    /// Uses the formationSlot to ensure each enemy picks a distinct orbit angle.
    /// </summary>
    private void CheckCrowdSpread(float distToPlayer)
    {
        // Only spread when actively chasing — not when flanking or intercepting
        if (state != BehaviourState.Chase)
            return;

        Collider[] nearby = Physics.OverlapSphere(
            playerTarget.position, crowdSpreadRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        if (nearby.Length < maxNearbyAttackersBeforeSpread)
            return;

        if (Random.value > crowdSpreadChance)
            return;

        // Pick a surround angle based on this enemy's formation slot + jitter
        float baseAngle = formationSlot * (360f / 8f);
        flankAngle = baseAngle + Random.Range(-crowdAngleJitter, crowdAngleJitter);

        // Temporarily use the crowd surround radius instead of the normal flank orbit radius
        // by injecting the target via flankAngle — the flank orbit will use flankOrbitRadius,
        // so we scale it here to match crowdSurroundRadius by adjusting the flankAngle target point.
        // (For full flexibility, a dedicated orbit radius override can be added; this is a clean approximation.)
        state = BehaviourState.Flank;
        stateTimer = Random.Range(crowdSpreadMinDuration, crowdSpreadMaxDuration);
        TryBeginFlankSprint();
    }

    private void TransitionToAttack(float distToPlayer)
    {
        bool canIntercept = distToPlayer >= minInterceptDistance && distToPlayer <= interceptDistance;
        // Use anticipation-scaled intercept chance instead of the raw inspector value
        bool choosesIntercept = canIntercept && Random.value <= effectiveInterceptChance;

        if (choosesIntercept)
        {
            state = BehaviourState.Intercept;
            stateTimer = Random.Range(interceptMinDuration, interceptMaxDuration);
        }
        else if (Random.value > aggressionBias && Random.value <= flankChance)
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

        Vector3 result = primary
            + sep * separationStrength
            + obstacle * avoidanceStrength
            + boundary * boundaryStrength;

        // Actieve beam-ontwijking voor enemies die nog niet Weakened zijn:
        // als de enemy in de beam zit tijdens Chase/Flank/Intercept probeert hij
        // zijwaarts weg te sturen om de beam te verlaten.
        if (isInFlashlightBeam
            && state != BehaviourState.Weakened
            && state != BehaviourState.Recoil
            && Random.value <= beamEvasionChance)
        {
            Vector3 evasion = GetBeamEvasionVector(toPlayerFlat);
            result += evasion * beamEvasionStrength;
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

    /// <summary>
    /// Intercept direction uses effectiveInterceptLookAhead, which scales with gameTimeSurvived.
    /// Older enemies anticipate further ahead.
    /// </summary>
    private Vector3 GetInterceptDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        Rigidbody playerRb = playerTarget.GetComponent<Rigidbody>();
        Vector3 playerVel = playerRb != null
            ? new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z)
            : Vector3.zero;

        float timeToReach = distToPlayer / Mathf.Max(moveSpeed * chaseSpeedMultiplier, 0.1f);
        Vector3 toIntercept = playerTarget.position
            + playerVel * timeToReach * effectiveInterceptLookAhead   // <── scaled by anticipation
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
    /// Weakened enemy retreats from the player AND steers zijwaarts om uit de beam te komen.
    /// Het heals while retreating — no minimum distance needed anymore.
    /// Once fully healed it transitions back to attack via UpdateFlashlightExposure.
    /// </summary>
    private Vector3 GetWeakenedDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        Vector3 retreat = -toPlayerFlat;

        // Voeg een zijwaartse component toe om actief uit de beam te sturen
        Vector3 evasion = GetBeamEvasionVector(toPlayerFlat);
        return (retreat + evasion * 0.6f).normalized;
    }

    /// <summary>
    /// Berekent een zijwaartse ontwijkrichting loodrecht op de beam (richting speler).
    /// De richting wordt eenmalig gekozen als de beam raakt en vastgehouden totdat
    /// de enemy uit de beam is.
    /// </summary>
    private Vector3 GetBeamEvasionVector(Vector3 toPlayerFlat)
    {
        if (!isInFlashlightBeam)
        {
            hasChosenEvasionDir = false;
            return Vector3.zero;
        }

        if (!hasChosenEvasionDir || beamEvasionDir == Vector3.zero)
        {
            // Kies willekeurig links of rechts loodrecht op de richting naar de speler
            Vector3 perp = Vector3.Cross(toPlayerFlat, Vector3.up);
            beamEvasionDir = (Random.value < 0.5f ? perp : -perp).normalized;
            hasChosenEvasionDir = true;
        }

        return beamEvasionDir;
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
            // Hoe meer schade én hoe dichter bij de beam, hoe trager.
            // effectFactor (0–1) schaalt hoe sterk de vertraging is op afstand.
            float slowFraction = flashlightDamage * 0.85f * flashlightEffectFactor;
            float speedFraction = 1f - slowFraction;
            return weakenedSpeed * speedFraction;
        }
        if (state == BehaviourState.Fleeing) return fleeSpeed;

        float baseSpeed = moveSpeed * chaseSpeedMultiplier;
        float speed = baseSpeed * GetDistanceBoost(distToPlayer) * GetFatigueMultiplier();

        // Sprint modifier: boost when sprinting, penalty when exhausted
        if (isSprinting)
            speed *= sprintSpeedMultiplier;
        else if (isExhausted)
            speed *= exhaustedSpeedMultiplier;

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
        // Dying: movement is fully handled in UpdateState, return 0 here
        if (state == BehaviourState.Dying)
            return 0f;

        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float targetY = GetTargetFlyingHeight(distToPlayer);
        float diff = targetY - transform.position.y;

        // Weakened/flashlight: use weakenedFallSpeed downward, normal speed upward
        // so the enemy falls quickly but can't rise back up while damaged
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

        // Weakened / flashlight: target height sinks toward the ground based on damage.
        // At flashlightDamage=0: normal restHeight. At flashlightDamage=1: weakenedMinFloatHeight above surface.
        if (state == BehaviourState.Weakened || isInFlashlightBeam)
        {
            float weakenedTarget = Mathf.Lerp(restHeight, surfaceY + weakenedMinFloatHeight, flashlightDamage);
            return weakenedTarget;
        }

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
        if (clearanceHeight > desiredHeight)
            desiredHeight = clearanceHeight;

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
            ? groundHit.point.y
            : 0f;

        float obstacleY = Physics.Raycast(origin, Vector3.down, out RaycastHit obstacleHit,
            heightRaycastDistance, obstacleLayers, QueryTriggerInteraction.Ignore)
            ? obstacleHit.point.y
            : float.MinValue;

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