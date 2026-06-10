using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    private enum BehaviourState
    {
        Inactive, Chase, Flank, Intercept, Lunge, Recoil,
        Retreat, Weakened, Fleeing, Dying,
        MistEntry, // achteraan zodat bestaande animator-int-waarden niet verschuiven
    }

    private static int formationCounter;
    private static readonly List<EnemyController> _allActive = new List<EnemyController>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        formationCounter = 0;
        _allActive.Clear();
    }

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
    [SerializeField] private float maxBoostMultiplier = 1.3f;

    [Header("Surround & Flank")]
    [SerializeField] private float flankDuration = 12f;
    [SerializeField] private float flankOrbitRadius = 5f;
    [SerializeField] private float flankOrbitRadiusEarlyWave = 10f;
    [SerializeField] private float flankOrbitSpeed = 1.2f;
    [SerializeField] private float flankSideFlipInterval = 4f;
    [SerializeField, Range(0f, 1f)] private float visibilityDotThreshold = 0.5f;

    [Header("Intercept")]
    [SerializeField] private float interceptDistance = 20f;
    [SerializeField] private float minInterceptDistance = 10f;
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

    [Header("Coordinated Attack")]
    [SerializeField] private float coordAttackRadius = 20f;

    [Header("Enemy Separation")]
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
    [Tooltip("Minimale afstand tot de mist muur die geesten bewaren nadat ze binnen zijn (units).")]
    [SerializeField] private float mistWallKeepoutDistance = 10f;

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

    [Header("Weakened Effects")]
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

    [Header("Flashlight / Health")]
    [SerializeField] private float flashlightKillTime = 2.4f;
    [SerializeField] private float healTime = 5f;
    [SerializeField] private float partialHealTimeMultiplier = 2.5f;
    [Tooltip("Hoe veel sneller geesten healen per wave (0.08 = ~70% sneller op wave 10).")]
    [SerializeField] private float healSpeedPerWave = 0.08f;

    [Header("Beam Resistance")]
    [Tooltip("Fractie schade (0–1) die een geest moet opstapelen vóórdat hij Weakened wordt.")]
    [SerializeField, Range(0f, 0.5f)] private float beamWeakenThreshold = 0.22f;
    [Tooltip("Maximum schade die een geest kan oplopen VOORDAT hij Weakened is. " +
             "Boven deze waarde moet hij eerst verzwakt worden om te sterven (0.80 = bijna dood, maar net niet).")]
    [SerializeField, Range(0.5f, 1f)] private float weakenedKillCap = 0.80f;
    [Tooltip("Kans dat een agressieve geest de verzwakking negeert en doorcharget naar de speler.")]
    [SerializeField, Range(0f, 1f)]   private float beamPushThroughChance = 0.30f;
    [Tooltip("Radius waarbinnen nabije geesten een opportunistische Chase starten als één geest Weakened gaat.")]
    [SerializeField] private float groupOpportunismRadius = 14f;
    [Tooltip("Kans per nabije geest dat hij de opportunistische Chase joinen.")]
    [SerializeField, Range(0f, 1f)] private float groupOpportunismChance = 0.60f;

    [Header("Beam Evasion")]
    [SerializeField] private float beamEvasionStrength = 6f;
    [SerializeField, Range(0f, 1f)] private float beamEvasionChance = 0.85f;

    [Header("Group Shield")]
    [Tooltip("Radius waarbinnen bondgenoten meetellen voor het groepsschild.")]
    [SerializeField] private float groupShieldRadius = 6f;
    [Tooltip("Minimaal aantal nabije bondgenoten voor een schild-effect.")]
    [SerializeField] private int groupShieldMinAllies = 3;
    [Tooltip("Aantal bondgenoten waarbij het schild maximaal sterk is.")]
    [SerializeField] private int groupShieldMaxAllies = 6;
    [Tooltip("Maximale damage-reductie (0.4 = 40% minder zaklamp-schade in groep).")]
    [SerializeField, Range(0f, 0.85f)] private float groupShieldMaxReduction = 0.40f;

    [Header("Battery Behaviour")]
    [Tooltip("Batterij-fractie waaronder geesten extra agressief worden.")]
    [SerializeField, Range(0f, 0.5f)] private float batteryEmptyThreshold = 0.20f;
    [Tooltip("Extra DynamicBonus-waarde als de batterij leeg is (optelt bij wave-bonus).")]
    [SerializeField] private float batteryEmptyAggressionBonus = 0.50f;
    [Tooltip("Extra push-through kans als de batterij leeg is.")]
    [SerializeField, Range(0f, 0.5f)] private float batteryEmptyPushThroughBonus = 0.30f;
    [Tooltip("Batterij-fractie waarboven geesten gaan lokken.")]
    [SerializeField, Range(0.5f, 1f)] private float batteryFullThreshold = 0.80f;
    [Tooltip("Batterij-fractie waaronder luring stopt en geest agressief wordt.")]
    [SerializeField, Range(0.3f, 0.8f)] private float batteryLureStopThreshold = 0.55f;
    [Tooltip("Kans dat een geest in lokmodus gaat als de batterij vol is (wave-agressie < 0.6).")]
    [SerializeField, Range(0f, 1f)] private float batteryFullLureChance = 0.40f;
    [Tooltip("Orbit-straal tijdens lokmodus (groot, zichtbaar voor de speler).")]
    [SerializeField] private float lureOrbitRadius = 14f;
    [Tooltip("Extra zichtbaarheid voor lokgeesten zodat de speler ze opmerkt.")]
    [SerializeField, Range(0f, 0.4f)] private float lureVisibilityBoost = 0.12f;

    [Header("Aggression Scaling")]
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

    [Header("Crowd Spreading")]
    [SerializeField] private float crowdCheckInterval = 0.7f;
    [SerializeField] private float crowdSpreadRadius = 8f;
    [SerializeField] private int maxNearbyAttackersBeforeSpread = 5;
    [SerializeField, Range(0f, 1f)] private float crowdSpreadChance = 0.65f;
    [SerializeField] private float crowdSpreadMinDuration = 4f;
    [SerializeField] private float crowdSpreadMaxDuration = 8f;
    [SerializeField] private float crowdAngleJitter = 35f;

    [Header("Intercept Lookahead Scaling")]
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
    [SerializeField, Range(0f, 1f)] private float baseVisibility = 0.02f;
    [SerializeField] private float enemyVisibilityDistance = 40f;

    [Header("Attack Materialization")]
    [SerializeField] private float materializationRange = 8f;
    [SerializeField] private float materializationDuration = 2f;

    [Header("Mist Entry")]
    [Tooltip("Afstand tot de kaartrand waarbinnen de geest zichtbaar wordt en de mistwand opent (units).")]
    [SerializeField] private float mistEntryVisibilityRange = 10f;

    [Header("Wave Retreat")]
    [SerializeField] private float maxRetreatDuration = 7f;
    [SerializeField] private float minRetreatDuration = 1.5f;
    [SerializeField] private float retreatSpeed = 9f;

    // ── Runtime state ────────────────────────────────────────────────────────

    private Rigidbody rb;
    private Transform playerTarget;
    private Transform playerCamera;
    private GhostClothSetup ghostClothSetup;
    private Animator animator;

    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashState = Animator.StringToHash("State");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsWeakened = Animator.StringToHash("IsWeakened");
    private static readonly int HashIsDying = Animator.StringToHash("IsDying");
    private static readonly int HashRecoil = Animator.StringToHash("Recoil");

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

    private float effectiveInterceptLookAhead;

    private float sprintStamina;
    private bool isSprinting;
    private bool isExhausted;

    private float _targetVisibility = 0f;
    private float _currentVisibility = 0f;
    private float _materializationProgress = 0f;
    private Collider _mainCollider;
    private Collider _playerCollider;

    private float waveAggressionLevel = 0f;
    private int currentWaveNumber = 0;
    private float _baseSpeed; // Inspector-waarde vóór alle multipliers, voor speed cap
    private float retreatTimer;
    private Vector3 retreatDirection;

    private bool _isScoutMode = false;
    private bool _isPowerUpCarrier = false;
    private bool _isAttackModeVisible = false;
    private Vector3 _baseScale; // basisschaal vóór alle mode- en aggression-scalings

    private bool _hasPassedMistWall = false;
    private bool _isMistEntryDisplacer = false;
    private float _stuckTimer = 0f;

    private bool _isFlyingOut;  // vlucht buiten de kaart (despawn als buiten de rand)
    private bool _isLuring;    // lokt de speler weg (grote orbit, meer zichtbaar)
    private float _groupShieldMult = 1f;
    private float _groupShieldTimer;

    // ── Gecoördineerde aanval ────────────────────────────────────────────────
    // Rol die deze geest krijgt tijdens een gecoördineerde aanval (0=direct, 1=links, 2=rechts, 3=achter)
    private int   _coordRole;
    private float _coordDelay;          // seconden voor activatie
    private Vector3 _coordPlayerFwd;    // speler-kijkrichting op moment van coördinatie

    // Ghost memory — succesvol healen vergroot ontwijkkans permanent
    private int   _evadeLearnCount     = 0;
    private int   _maxEvadeLearnCount  = 3;
    private float _evadeLearnIncrement = 0.10f;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _baseSpeed = moveSpeed; // sla originele Inspector-waarde op vóór welke multiplier dan ook
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        ghostClothSetup = GetComponent<GhostClothSetup>();
        animator = GetComponent<Animator>();
        _mainCollider = GetComponent<Collider>();

        moveSpeed *= DifficultySettings.EnemySpeedMultiplier;
        flashlightKillTime *= DifficultySettings.EnemyHealthMultiplier;

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

        effectiveInterceptLookAhead = interceptLookAheadMin;

        _baseScale = transform.localScale;
        state = BehaviourState.MistEntry; // begin altijd met door de mistwand laden
        // Registreer pas als displacer (mist-opening) als de geest dicht bij de wand is.
        _isMistEntryDisplacer = false;
    }

    private void OnEnable()
    {
        if (!_allActive.Contains(this)) _allActive.Add(this);
    }

    private void OnDisable()
    {
        _allActive.Remove(this);
        UnregisterMistDisplacer();
    }

    private void UnregisterMistDisplacer()
    {
        if (_isMistEntryDisplacer)
        {
            VolumetricMistController.Unregister(transform);
            _isMistEntryDisplacer = false;
        }
    }

    private void Start()
    {
        if (!PlayerFinder.TryAssignIfNull(ref playerTarget))
            Debug.LogWarning("EnemyController: No player found.");
        if (playerTarget != null)
            _playerCollider = playerTarget.GetComponentInChildren<Collider>();

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
        UpdateAnimator();

        wasInFlashlightBeamLastFrame = isInFlashlightBeam;
        isInFlashlightBeam = false;
        flashlightEffectFactor = 0f;
    }

    // ── Group broadcast ──────────────────────────────────────────────────────

    public static void BroadcastGroupAttack()
    {
        foreach (var enemy in _allActive)
            enemy.ForceChaseState();
    }

    private void ForceChaseState()
    {
        if (state == BehaviourState.Dying ||
            state == BehaviourState.Retreat ||
            state == BehaviourState.Weakened) return;

        state = BehaviourState.Chase;
        stateTimer = Random.Range(4f, 10f);
        isSprinting = false;
    }

    // ── Variant modes ────────────────────────────────────────────────────────

    public void SetEliteMode()
    {
        moveSpeed *= 0.75f;
        flashlightKillTime *= 2.5f;
        transform.localScale *= 1.5f;
        originalScale = transform.localScale;
        ghostClothSetup?.NotifyScaleChanged();
        _maxEvadeLearnCount  = 5;
        _evadeLearnIncrement = 0.18f;
    }

    public void SetScoutMode()
    {
        _isScoutMode = true;
        moveSpeed *= 1.4f;
        flashlightKillTime *= 0.4f;
        retreatSpeed *= 1.5f;
        transform.localScale *= 0.65f;
        originalScale = transform.localScale;
        ghostClothSetup?.SetScoutAppearance(true);
        ghostClothSetup?.NotifyScaleChanged();
    }

    public void SetPowerUpCarrierMode()
    {
        _isPowerUpCarrier = true;
        _isScoutMode = true;
        moveSpeed *= 0.55f;
        retreatSpeed *= 0.6f;
        lungeChance = 0f;
        flashlightKillTime *= 0.4f;
        transform.localScale *= 0.65f;
        originalScale = transform.localScale;
        ghostClothSetup?.SetScoutAppearance(true);
        ghostClothSetup?.SetPowerUpCarrierLight();
        ghostClothSetup?.NotifyScaleChanged();
    }

    /// <summary>
    /// Aanroepen als de geest al binnen de kaart spawnt (sla MistEntry over,
    /// activeer boundary-behoud zodat hij de fogmuur niet kan verlaten).
    /// </summary>
    public void SetSpawnedInsideMap()
    {
        _hasPassedMistWall = true;
        // Zorg dat de geest niet in MistEntry blijft hangen;
        // TransitionToAttack zet carriers meteen in Retreat (wandelen).
        if (state == BehaviourState.MistEntry || state == BehaviourState.Inactive)
            state = BehaviourState.Retreat;
    }

    public bool IsInAttackMode()
    {
        return state == BehaviourState.Chase  ||
               state == BehaviourState.Lunge  ||
               state == BehaviourState.Intercept ||
               state == BehaviourState.Flank;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void SetTarget(Transform target) => playerTarget = target;

    public void SetWaveData(int waveNumber, float aggressionLevel)
    {
        currentWaveNumber = waveNumber;
        waveAggressionLevel = aggressionLevel;

        // Basis agressiespectrum: hogere offset (-0.5 ipv -1) zodat geesten al op wave 1 agressiever zijn.
        // De wave-aggression factor is verhoogd (2.8x ipv 2x) voor een steilere curve.
        float baseSpectrum = Mathf.Lerp(-1f, 1f, (float)formationSlot / 7f)
            + Random.Range(-0.15f, 0.15f);
        aggressionSpectrum = Mathf.Clamp(baseSpectrum + aggressionLevel * 2.8f - 0.5f, -1f, 1f);

        // Basis lookahead via wave-aggressie; wordt verder verhoogd in UpdateState via dynamicBonus
        effectiveInterceptLookAhead = Mathf.Lerp(interceptLookAheadMin, interceptLookAheadMax, aggressionLevel);

        ApplyAggressionStats(aggressionLevel);
    }

    public void SetHordeMode()
    {
        moveSpeed *= 1.2f;
        transform.localScale *= 0.8f;
        originalScale = transform.localScale;
        ghostClothSetup?.NotifyScaleChanged();
        _maxEvadeLearnCount  = 2;
        _evadeLearnIncrement = 0.05f;
    }

    /// <summary>
    /// Harde ondergrens: geen geest mag kleiner zijn dan 80 % van zijn basis-prefabschaal.
    /// Aanroepen nadat alle mode-methoden zijn toegepast.
    /// </summary>
    public void EnforceMinimumScale()
    {
        Vector3 minScale = _baseScale * 0.8f;
        if (transform.localScale.x < minScale.x ||
            transform.localScale.y < minScale.y ||
            transform.localScale.z < minScale.z)
        {
            transform.localScale = Vector3.Max(transform.localScale, minScale);
            originalScale = transform.localScale;
            ghostClothSetup?.NotifyScaleChanged();
        }
    }

    /// <summary>Roep aan vanuit EnemySpawner om de geest dynamisch naar buiten te laten vliegen.</summary>
    public void BeginFlyOut()
    {
        if (state == BehaviourState.Dying) return;

        _isFlyingOut = true;
        state = BehaviourState.Fleeing;
        stateTimer = 15f;

        // Begin meteen te vervagen zodat de geest onzichtbaar is terwijl hij wegvliegt
        _targetVisibility = 0f;
        _materializationProgress = 0f;

        Vector3 center = MapController.Instance != null
            ? MapController.Instance.PlatformCenter
            : Vector3.zero;
        Vector3 awayDir = transform.position - center;
        awayDir.y = 0f;
        fleeDirection = awayDir.sqrMagnitude > 0.01f ? awayDir.normalized : transform.forward;
    }

    public void ApplyMultipliers(float healthMult, float speedMult, bool isVisible = false)
    {
        moveSpeed *= speedMult;
        moveSpeed = Mathf.Min(moveSpeed, _baseSpeed * 2f); // max 2× originele snelheid
        float effectiveHealthMult = isVisible ? healthMult * 0.6f : healthMult;
        flashlightKillTime *= effectiveHealthMult;
        flashlightKillTime = Mathf.Max(0.3f, flashlightKillTime);
        _isAttackModeVisible = isVisible;
    }

    private void ApplyAggressionStats(float antT = 0f)
    {
        float t = (aggressionSpectrum + 1f) * 0.5f;

        moveSpeed *= Mathf.Lerp(speedMultiplierMin, speedMultiplierMax, t);
        moveSpeed = Mathf.Min(moveSpeed, _baseSpeed * 2f); // cap ook na aggression scaling
        damagePercentage *= Mathf.Lerp(damageMultiplierMin, damageMultiplierMax, t);
        lungeTriggerDistance *= Mathf.Lerp(lungeDistanceMultiplierMin, lungeDistanceMultiplierMax, t);
        lungeSpeed *= Mathf.Lerp(lungeSpeedMultiplierMin, lungeSpeedMultiplierMax, t);

        float scaleFactor = Mathf.Lerp(scaleAtMinAggression, scaleAtMaxAggression, t);
        originalScale = transform.localScale * scaleFactor;
        // Geesten mogen maximaal 0.8× zo klein worden als hun basisschaal
        originalScale = Vector3.Max(originalScale, _baseScale * 0.8f);
        transform.localScale = originalScale;

        float cooldownReduction = maxLungeCooldownReduction * antT;
        lungeCooldownTimer.Reset(lungeCooldown * (1f - cooldownReduction));

        ghostClothSetup?.NotifyScaleChanged();
    }

    public void TakeRecoil(Vector3 hitDirection)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing) return;

        recoilDir = new Vector3(-hitDirection.x, 0f, -hitDirection.z).normalized;
        recoilTimer = recoilDuration;
        state = BehaviourState.Recoil;

        if (animator != null) animator.SetTrigger(HashRecoil);
    }

    public void SetTargetVisibility(float visibility) => _targetVisibility = visibility;

    /// <summary>
    /// Verdeelt alle actieve geesten over vier aanvalsrollen en geeft elk een
    /// gestaggerde activatievertraging zodat ze nooit allemaal tegelijk aanvallen.
    /// Rol 0 = directe chase (dichtstbij), 1 = links, 2 = rechts, 3 = achter.
    /// </summary>
    private void AssignCoordinatedRoles()
    {
        if (playerTarget == null) return;

        // Bepaal speler-kijkrichting (plat, geen Y)
        Vector3 playerFwd = playerCamera != null
            ? new Vector3(playerCamera.forward.x, 0f, playerCamera.forward.z).normalized
            : transform.forward;

        // Bouw pool van beschikbare geesten (niet zichzelf, niet stervend/weakened/vluchtend)
        var pool = new System.Collections.Generic.List<EnemyController>();
        foreach (var e in _allActive)
        {
            if (e.rb == rb) continue;
            if (e.state == BehaviourState.Dying    ||
                e.state == BehaviourState.Weakened  ||
                e.state == BehaviourState.Fleeing   ||
                e.state == BehaviourState.Lunge) continue;
            pool.Add(e);
        }
        if (pool.Count == 0) return;

        // Sorteer: dichtstbij de speler krijgt rol 0 (direct aanval)
        Vector3 pPos = playerTarget.position;
        pool.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - pPos)
            .CompareTo(Vector3.SqrMagnitude(b.transform.position - pPos)));

        // Wijs rollen toe en stagger de activatie
        // Rol 0 (direct)  : 0.0–0.4 s  → valt meteen aan na de lunger
        // Rol 1 (links)   : 1.0–1.8 s  → flankt van linkerkant terwijl 0 afleidt
        // Rol 2 (rechts)  : 1.5–2.4 s  → flankt van rechterkant
        // Rol 3 (achter)  : 2.5–3.5 s  → sluipt van achteren als de speler druk zet
        float[] baseDelays = { 0f, 1.0f, 1.5f, 2.5f };
        float[] jitter     = { 0.4f, 0.8f, 0.9f, 1.0f };

        for (int i = 0; i < pool.Count; i++)
        {
            int role  = i % 4;
            float delay = baseDelays[role] + Random.Range(0f, jitter[role]);
            pool[i].ReceiveCoordinatedRole(role, delay, playerFwd);
        }
    }

    /// <summary>Ontvang een aanvalsrol en activeer die na <paramref name="delay"/> seconden.</summary>
    public void ReceiveCoordinatedRole(int role, float delay, Vector3 playerForward)
    {
        if (state == BehaviourState.Dying    ||
            state == BehaviourState.Weakened  ||
            state == BehaviourState.Fleeing   ||
            state == BehaviourState.Lunge) return;

        _coordRole      = role;
        _coordDelay     = Mathf.Max(delay, 0f);
        _coordPlayerFwd = playerForward.sqrMagnitude > 0.01f ? playerForward : Vector3.forward;
    }

    private void ActivateCoordinatedRole()
    {
        // Bereken de basishoek vanuit speler-kijkrichting
        float fwdDeg = Mathf.Atan2(_coordPlayerFwd.x, _coordPlayerFwd.z) * Mathf.Rad2Deg;

        switch (_coordRole)
        {
            case 0: // Directe frontale aanval — sprint recht op speler af
                state = BehaviourState.Chase;
                stateTimer = Random.Range(4f, 7f);
                if (!isExhausted) isSprinting = true;
                break;

            case 1: // Links flank — nadert van de linkerkant van de speler (~90°)
                flankAngle = fwdDeg + 85f + Random.Range(-25f, 25f);
                state      = BehaviourState.Flank;
                stateTimer = Random.Range(6f, 11f);
                TryBeginFlankSprint();
                break;

            case 2: // Rechts flank — nadert van de rechterkant (~-90°)
                flankAngle = fwdDeg - 85f + Random.Range(-25f, 25f);
                state      = BehaviourState.Flank;
                stateTimer = Random.Range(6f, 11f);
                TryBeginFlankSprint();
                break;

            case 3: // Achterkant — sluipt van achter de speler (~180°)
                flankAngle = fwdDeg + 175f + Random.Range(-20f, 20f);
                state      = BehaviourState.Flank;
                stateTimer = Random.Range(8f, 14f);
                TryBeginFlankSprint();
                break;
        }
    }

    // Wordt aangeroepen door een andere geest die Lunge activeert
    public void JoinCoordinatedAttack()
    {
        if (state == BehaviourState.Inactive ||
            state == BehaviourState.Dying ||
            state == BehaviourState.Weakened ||
            state == BehaviourState.Lunge ||
            state == BehaviourState.Retreat) return;

        float joinChance = Mathf.Lerp(0.25f, 0.85f, waveAggressionLevel);
        if (Random.value > joinChance) return;

        state = BehaviourState.Chase;
        stateTimer = Random.Range(5f, 10f);
    }

    // ── Flashlight ───────────────────────────────────────────────────────────

    public void ReceiveFlashlightHit(float effectFactor = 1f, Vector3 beamDirection = default, bool spotOnly = false)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing) return;

        // spotOnly = zwakke modus: alleen zichtbaar maken, geen damage of vertraging
        if (spotOnly) return;

        isInFlashlightBeam = true;
        flashlightEffectFactor = Mathf.Max(flashlightEffectFactor, effectFactor);

        if (beamDirection != Vector3.zero)
            flashlightBeamDirection = beamDirection;

        if (!wasInFlashlightBeamLastFrame)
        {
            isSprinting = false;

            if (state == BehaviourState.Lunge)
                lungeMoveVelocity = Vector3.zero;

            // ── Beam resistance buffer ────────────────────────────────────────
            // Geesten worden pas Weakened nadat ze beamWeakenThreshold schade opgebouwd hebben.
            // Dit geeft ze een aanvalsvenster bij kort zaklamplicht en verhindert instant-stop.
            float effectiveThreshold = beamWeakenThreshold
                * Mathf.Lerp(1.4f, 0.5f, (aggressionSpectrum + 1f) * 0.5f); // agressieve geesten drempel lager
            if (flashlightDamage < effectiveThreshold) return;

            // ── Push-through mechanic ─────────────────────────────────────────
            // Agressieve geesten hebben kans om de beam te negeren en door te chargen.
            float aggrT = (aggressionSpectrum + 1f) * 0.5f;
            float dynamicPushBonus = WaveManager.Instance != null
                ? WaveManager.Instance.DynamicAggressionBonus * 0.4f : 0f;
            // Lege batterij → geesten forceren zich door de beam heen
            float battFrac = BatteryController.Instance != null ? BatteryController.Instance.BatteryFraction : 1f;
            if (battFrac < batteryEmptyThreshold)
            {
                float emptyT = 1f - battFrac / Mathf.Max(batteryEmptyThreshold, 0.01f);
                dynamicPushBonus += batteryEmptyPushThroughBonus * emptyT;
            }
            float effectivePushChance = beamPushThroughChance
                * Mathf.Lerp(0f, 1f, aggrT)
                * (1f + dynamicPushBonus);

            if (Random.value < effectivePushChance)
            {
                // Geest charget door — sprint naar speler, geen Weakened
                state = BehaviourState.Chase;
                isSprinting = true;
                stateTimer = Random.Range(2f, 4f);
                TriggerGroupOpportunism(); // nabije geesten zien dit als signaal om mee aan te vallen
                return;
            }

            EnterWeakened();
            TriggerGroupOpportunism(); // speler is afgeleid → nabije geesten zien hun kans
        }
    }

    /// <summary>
    /// Stuurt nabije geesten een Chase-signaal: de speler is bezig met één geest,
    /// waardoor anderen hun kans zien om in te vallen.
    /// </summary>
    private void TriggerGroupOpportunism()
    {
        if (playerTarget == null) return;

        Vector3 playerFwd = playerCamera != null
            ? new Vector3(playerCamera.forward.x, 0f, playerCamera.forward.z).normalized
            : Vector3.forward;

        Collider[] nearby = Physics.OverlapSphere(
            transform.position, groupOpportunismRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        // Bouw pool en wijs gespreide rollen toe (zelfde systeem als gecoördineerde aanval)
        var pool = new System.Collections.Generic.List<EnemyController>();
        foreach (Collider col in nearby)
        {
            if (col.attachedRigidbody == rb) continue;
            var other = col.GetComponentInParent<EnemyController>();
            if (other == null || Random.value > groupOpportunismChance) continue;
            pool.Add(other);
        }

        float[] baseDelays = { 0f, 0.6f, 1.0f, 1.8f };
        float[] jitter     = { 0.3f, 0.5f, 0.6f, 0.7f };
        for (int i = 0; i < pool.Count; i++)
        {
            int role  = i % 4;
            float delay = baseDelays[role] + Random.Range(0f, jitter[role]);
            pool[i].ReceiveCoordinatedRole(role, delay, playerFwd);
        }
    }

    /// <summary>
    /// Aangeroepen door een nabije geest die Weakened of push-through triggert.
    /// De roepende geest is de afleiding — deze geest pakt zijn kans om aan te vallen.
    /// </summary>
    public void JoinOpportunisticChase()
    {
        if (state == BehaviourState.Dying    ||
            state == BehaviourState.Fleeing  ||
            state == BehaviourState.Weakened ||
            state == BehaviourState.Lunge) return;

        state = BehaviourState.Chase;
        stateTimer = Random.Range(3f, 6f);
        if (!isExhausted) isSprinting = true;
    }

    private void UpdateFlashlightExposure(float dt)
    {
        if (state == BehaviourState.Dying || state == BehaviourState.Fleeing) return;

        bool activeBeam = isInFlashlightBeam || wasInFlashlightBeamLastFrame;

        if (!activeBeam)
            flashlightEffectFactor = 0f;

        // Ververs het groepsschild periodiek (goedkoper dan elke FixedUpdate)
        _groupShieldTimer -= dt;
        if (_groupShieldTimer <= 0f)
        {
            _groupShieldTimer = 0.4f;
            _groupShieldMult = ComputeGroupShieldMultiplier();
        }

        if (activeBeam)
        {
            flashlightDamage += dt / flashlightKillTime * flashlightEffectFactor * _groupShieldMult;

            if (state == BehaviourState.Weakened)
            {
                flashlightDamage = Mathf.Clamp01(flashlightDamage);
                if (flashlightDamage + _materializationProgress >= 1f)
                    BeginDying();
            }
            else
            {
                // Geest kan bijna dood gaan maar heeft dat laatste stukje Weakened nodig
                flashlightDamage = Mathf.Min(flashlightDamage, weakenedKillCap);
            }
        }
        else if (state == BehaviourState.Weakened && flashlightDamage > 0f)
        {
            float spectrumHealMultiplier = Mathf.Lerp(3.5f, 0.5f, (aggressionSpectrum + 1f) * 0.5f);
            int currentWave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 1;
            float waveHealBonus = 1f / (1f + (currentWave - 1) * healSpeedPerWave);
            flashlightDamage -= dt / (healTime * partialHealTimeMultiplier * spectrumHealMultiplier * waveHealBonus)
                                * DifficultySettings.EnemyHealSpeedMultiplier;
            flashlightDamage = Mathf.Max(0f, flashlightDamage);

            if (flashlightDamage <= 0f)
            {
                LearnEvasion();
                float dist = playerTarget != null
                    ? Vector3.Distance(transform.position, playerTarget.position)
                    : 0f;
                TransitionToAttack(dist);
            }
        }
        else if (flashlightDamage > 0f)
        {
            // Passieve afname van opgebouwde schade als de geest niet in de beam is en niet Weakened
            // Zorgt dat korte flitsen niet permanent opstapelen
            flashlightDamage -= dt / (healTime * 3f) * DifficultySettings.EnemyHealSpeedMultiplier;
            flashlightDamage = Mathf.Max(0f, flashlightDamage);
        }
    }

    private float ComputeGroupShieldMultiplier()
    {
        if (groupShieldRadius <= 0f || groupShieldMaxReduction <= 0f) return 1f;

        Collider[] nearby = Physics.OverlapSphere(
            transform.position, groupShieldRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        int allyCount = 0;
        foreach (Collider col in nearby)
        {
            if (col.attachedRigidbody == rb) continue;
            var other = col.GetComponentInParent<EnemyController>();
            if (other != null && other.state != BehaviourState.Dying && other.state != BehaviourState.Fleeing)
                allyCount++;
        }

        if (allyCount < groupShieldMinAllies) return 1f;

        float t = Mathf.Clamp01(
            (float)(allyCount - groupShieldMinAllies) / Mathf.Max(1, groupShieldMaxAllies - groupShieldMinAllies));
        return 1f - groupShieldMaxReduction * t;
    }

    private void LearnEvasion()
    {
        if (_evadeLearnCount >= _maxEvadeLearnCount) return;
        int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 1;
        float waveScale = 1f + (wave - 1) * 0.05f;
        beamEvasionChance = Mathf.Clamp01(beamEvasionChance + _evadeLearnIncrement * waveScale);
        _evadeLearnCount++;
    }

    private void EnterWeakened()
    {
        if (state == BehaviourState.Dying) return;
        _isLuring = false;
        state = BehaviourState.Weakened;
    }

    private void BeginDying()
    {
        state = BehaviourState.Dying;
        deathTimer = deathRiseDuration;
        rb.linearVelocity = Vector3.zero;
        WaveManager.Instance?.NotifyEnemyKilled();
    }

    // ── Materialization ──────────────────────────────────────────────────────

    private void UpdateMaterialization(float dt)
    {
        bool isDirectAttack = state == BehaviourState.Chase || state == BehaviourState.Lunge;

        // Flank/Intercept materialise too, but only when very close to the player
        bool isCloseApproach = (state == BehaviourState.Flank || state == BehaviourState.Intercept)
            && playerTarget != null
            && Vector3.Distance(transform.position, playerTarget.position) <= attackDistance * 2.5f;

        bool shouldMaterialize = playerTarget != null
            && (isDirectAttack || isCloseApproach)
            && Vector3.Distance(transform.position, playerTarget.position) <= materializationRange;

        float direction = shouldMaterialize ? 1f : -1f;
        _materializationProgress = Mathf.Clamp01(
            _materializationProgress + direction * dt / materializationDuration);

        if (_mainCollider != null && _playerCollider != null)
            Physics.IgnoreCollision(_mainCollider, _playerCollider, _materializationProgress == 0f);
    }

    // ── Visibility ───────────────────────────────────────────────────────────

    private void UpdateVisibility()
    {
        if (state == BehaviourState.MistEntry)
        {
            _currentVisibility = 0f;
            ghostClothSetup?.ForceInvisible();
            return;
        }

        // Wegvliegende geesten vervagen snel en volledig (omzeilt scout-minimum in SetVisibility)
        if (_isFlyingOut)
        {
            _currentVisibility = Mathf.MoveTowards(_currentVisibility, 0f, Time.fixedDeltaTime * 0.33f);
            if (_currentVisibility <= 0f)
                ghostClothSetup?.ForceInvisible();
            else
                ghostClothSetup?.SetVisibilityDirect(_currentVisibility);
            return;
        }

        float damageOpacity = Mathf.Clamp01(flashlightDamage + _materializationProgress);

        float naturalBase = state == BehaviourState.Retreat ? 0f : _materializationProgress;
        float ambientTarget = Mathf.Max(_targetVisibility, naturalBase);

        // Lokgeesten geven zichzelf een kleine gloed — zichtbaar genoeg om op te vallen
        if (_isLuring)
            ambientTarget = Mathf.Max(ambientTarget, baseVisibility + lureVisibilityBoost);

        if (playerTarget != null)
        {
            float dist = Vector3.Distance(transform.position, playerTarget.position);
            float fogFactor = 1f - Mathf.Clamp01(
                Mathf.InverseLerp(enemyVisibilityDistance * 0.4f, enemyVisibilityDistance, dist));
            ambientTarget *= fogFactor;
        }

        float effectiveTarget = Mathf.Max(ambientTarget, damageOpacity);
        _currentVisibility = Mathf.Lerp(
            _currentVisibility, effectiveTarget, Time.fixedDeltaTime * visibilityFadeSpeed);
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
            state == BehaviourState.Retreat ||
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

    // ── Dynamic aggression helpers ────────────────────────────────────────────

    /// <summary>
    /// Runtime aggressiebonus van WaveManager (kills + wave-voortgang + difficulty).
    /// Wordt elke frame gelezen zodat geesten mid-wave slimmer worden.
    /// </summary>
    private float GetDynamicBonus()
    {
        float bonus = WaveManager.Instance != null ? WaveManager.Instance.DynamicAggressionBonus : 0f;
        float battFrac = BatteryController.Instance != null ? BatteryController.Instance.BatteryFraction : 1f;
        if (battFrac < batteryEmptyThreshold)
        {
            float emptyT = 1f - battFrac / Mathf.Max(batteryEmptyThreshold, 0.01f);
            bonus += batteryEmptyAggressionBonus * emptyT;
        }
        return bonus;
    }

    // ── State machine ────────────────────────────────────────────────────────

    private void UpdateState(float dt)
    {
        float distToPlayer = playerTarget != null
            ? Vector3.Distance(transform.position, playerTarget.position)
            : 0f;

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

            if (_isFlyingOut && MapController.Instance != null)
            {
                Vector3 center = MapController.Instance.PlatformCenter;
                float flatDist = new Vector2(
                    transform.position.x - center.x,
                    transform.position.z - center.z).magnitude;
                if (flatDist >= MapController.Instance.CurrentRadius + 5f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (stateTimer <= 0f)
            {
                if (_isFlyingOut)
                    Destroy(gameObject);
                else
                    BeginDying();
            }
            return;
        }

        if (state == BehaviourState.Weakened) return;

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

        if (state == BehaviourState.MistEntry)
        {
            // Registreer als displacer (mist-opening) zodra de geest dicht bij de wand is
            if (!_isMistEntryDisplacer && IsNearMapWall(mistEntryVisibilityRange))
            {
                VolumetricMistController.Register(transform);
                _isMistEntryDisplacer = true;
            }

            if (IsInsideHardWall())
            {
                _hasPassedMistWall = true;
                UnregisterMistDisplacer();
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

        if (state == BehaviourState.Retreat)
        {
            if (_isPowerUpCarrier)
            {
                if (playerTarget != null && distToPlayer < 18f)
                {
                    retreatDirection = (transform.position - playerTarget.position).normalized;
                    retreatTimer = Mathf.Max(retreatTimer, 2f);
                }

                // Stuck-detection: als de carrier nauwelijks beweegt, kies nieuwe richting
                if (rb.linearVelocity.sqrMagnitude < 0.25f)
                {
                    _stuckTimer += dt;
                    if (_stuckTimer > 1.5f)
                    {
                        _stuckTimer = 0f;
                        TransitionToAttack(distToPlayer);
                        return;
                    }
                }
                else
                {
                    _stuckTimer = 0f;
                }
            }

            retreatTimer -= dt;
            if (retreatTimer <= 0f)
                TransitionToAttack(distToPlayer);
            return;
        }

        // ── Gecoördineerde aanval: activatievertraging aftikken ──────────────
        if (_coordDelay > 0f)
        {
            _coordDelay -= dt;
            if (_coordDelay <= 0f)
                ActivateCoordinatedRole();
        }

        // ── Lure-stop: batterij gezakt → lokgeest wordt agressief ──────────────
        if (_isLuring)
        {
            float battFracLure = BatteryController.Instance != null
                ? BatteryController.Instance.BatteryFraction : 1f;
            if (battFracLure < batteryLureStopThreshold)
            {
                _isLuring = false;
                state = BehaviourState.Chase;
                stateTimer = Random.Range(4f, 8f);
                if (!isExhausted) isSprinting = true;
            }
        }

        // ── Active attack states: Chase, Flank, Intercept ─────────────────────

        crowdCheckTimer -= dt;
        if (crowdCheckTimer <= 0f)
        {
            crowdCheckTimer = crowdCheckInterval;
            CheckCrowdSpread(distToPlayer);
        }

        float aggrT = (aggressionSpectrum + 1f) * 0.5f;
        float dynamicBonus = GetDynamicBonus();

        // Lungetrigger-radius groeit iets naarmate de wave vordert
        float effectiveLungeTrigger = lungeTriggerDistance
            * Mathf.Lerp(0.6f, 1.4f, aggrT)
            * (1f + dynamicBonus * 0.25f);

        // Lunge-kans schaalt met aggression, wave-niveau én de runtime-bonus (kills + wave-voortgang)
        float effectiveLungeChance = lungeChance
            * Mathf.Lerp(0.4f, 1.6f, aggrT)
            * Mathf.Lerp(0.04f, 1.8f, waveAggressionLevel)
            * (1f + dynamicBonus * 2.5f);

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
                {
                    float retreatChance = Mathf.Lerp(0.9f, 0.05f, waveAggressionLevel);
                    if (distToPlayer > attackDistance * 2f && Random.value < retreatChance)
                        BeginRetreat();
                    else
                        TransitionToAttack(distToPlayer);
                }
                break;

            case BehaviourState.Flank:
                if (stateTimer <= 0f)
                {
                    if (waveAggressionLevel < 0.4f)
                        TransitionToAttack(distToPlayer);
                    else
                    {
                        state = BehaviourState.Chase;
                        stateTimer = Random.Range(3f, 8f);
                    }
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
        if (_isPowerUpCarrier)
        {
            float angle = Random.Range(0f, 360f);
            retreatDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            // Bias richting weg van de mistwand zodat de carrier niet vastloopt aan de rand
            if (MapController.Instance != null)
            {
                Vector3 mapCenter = MapController.Instance.PlatformCenter;
                float safeRadius = MapController.Instance.CurrentRadius
                                 - MapController.Instance.HardWallInset - 10f;
                Vector3 pos = transform.position;
                float flatDist = new Vector2(pos.x - mapCenter.x, pos.z - mapCenter.z).magnitude;
                if (flatDist >= safeRadius)
                {
                    Vector3 toCenter = new Vector3(mapCenter.x - pos.x, 0f, mapCenter.z - pos.z).normalized;
                    retreatDirection = (retreatDirection + toCenter * 3f).normalized;
                }
            }

            retreatTimer = Random.Range(3f, 6f);
            state = BehaviourState.Retreat;
            return;
        }

        // Stop luring wanneer we een niet-Flank state ingaan
        _isLuring = false;

        bool canIntercept = distToPlayer >= minInterceptDistance && distToPlayer <= interceptDistance;

        // Met hogere dynamicBonus (kills + wave-voortgang) verschuiven geesten naar Chase/Intercept:
        // ze worden agressiever en minder geduldig met flanken.
        float dynamicBonusTrans = GetDynamicBonus();
        float effectiveAggression = Mathf.Clamp01(waveAggressionLevel + dynamicBonusTrans * 0.6f);

        // ── Lokmodus: als de batterij vol is en aggression laag, lokt een deel van de geesten ─
        float battFracTrans = BatteryController.Instance != null ? BatteryController.Instance.BatteryFraction : 1f;
        bool canLure = !_isScoutMode
            && battFracTrans >= batteryFullThreshold
            && waveAggressionLevel < 0.60f
            && Random.value < batteryFullLureChance;

        float wFlank = Mathf.Lerp(0.60f, 0.12f, effectiveAggression);
        float wIntercept = canIntercept ? Mathf.Lerp(0.30f, 0.22f, effectiveAggression) : 0f;
        float wChase = Mathf.Lerp(0.10f, 0.75f, effectiveAggression);

        float total = wFlank + wIntercept + wChase;
        float roll = Random.value * total;

        if (canLure)
        {
            // Geest trekt een grote, zichtbare baan om de speler te lokken
            _isLuring = true;
            state = BehaviourState.Flank;
            flankAngle = Random.Range(0f, 360f);
            stateTimer = Random.Range(15f, 25f);
            isSprinting = false; // lokgeesten bewegen traag en opvallend
        }
        else if (roll < wFlank)
        {
            state = BehaviourState.Flank;
            stateTimer = Mathf.Lerp(flankDuration * 1.6f, flankDuration, waveAggressionLevel)
                + Random.Range(-2f, 2f);
            TryBeginFlankSprint();
        }
        else if (roll < wFlank + wIntercept)
        {
            state = BehaviourState.Intercept;
            stateTimer = Random.Range(interceptMinDuration, interceptMaxDuration);
        }
        else
        {
            state = BehaviourState.Chase;
            stateTimer = Mathf.Lerp(2f, 8f, waveAggressionLevel) + Random.Range(-0.5f, 1f);
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

        // Verdeel alle geesten over aanvalsrollen met gestaggerde timing
        AssignCoordinatedRoles();
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
            // Na een lunge retreatet de geest even voor hij herneemt
            BeginRetreat();
        }
    }

    // ── Retreat ───────────────────────────────────────────────────────────────

    private void BeginRetreat()
    {
        if (state == BehaviourState.Dying ||
            state == BehaviourState.Fleeing ||
            state == BehaviourState.Weakened) return;

        _isLuring = false;
        state = BehaviourState.Retreat;

        float duration = Mathf.Lerp(maxRetreatDuration, minRetreatDuration, waveAggressionLevel);
        retreatTimer = duration * Random.Range(0.8f, 1.2f);

        if (playerTarget != null)
            retreatDirection = (transform.position - playerTarget.position).normalized;
        else
            retreatDirection = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;

        // Buig de retreatrichting licht zijwaarts van de lichtbundel zodat de geest niet
        // recht terug langs de beam vlucht – voelt naturaler en minder robotisch
        if (flashlightBeamDirection != Vector3.zero && (isInFlashlightBeam || flashlightDamage > 0f))
        {
            Vector3 beamFlat = new Vector3(flashlightBeamDirection.x, 0f, flashlightBeamDirection.z).normalized;
            Vector3 lateral  = Vector3.Cross(beamFlat, Vector3.up) * flankSide * 0.4f;
            retreatDirection = (retreatDirection + lateral).normalized;
        }

        retreatDirection.y = 0f;
        if (retreatDirection.sqrMagnitude < 0.01f)
            retreatDirection = -transform.forward;

        smoothedVelocity = Vector3.zero;
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

    // ── MistEntry helpers ────────────────────────────────────────────────────

    private bool IsInsideHardWall()
    {
        if (MapController.Instance == null) return true;
        float hardWallRadius = MapController.Instance.CurrentRadius - MapController.Instance.HardWallInset;
        Vector3 center = MapController.Instance.PlatformCenter;
        float flatDist = new Vector2(
            transform.position.x - center.x,
            transform.position.z - center.z).magnitude;
        return flatDist <= hardWallRadius;
    }

    private bool IsNearMapWall(float range)
    {
        if (MapController.Instance == null) return true;
        Vector3 center = MapController.Instance.PlatformCenter;
        float flatDist = new Vector2(
            transform.position.x - center.x,
            transform.position.z - center.z).magnitude;
        return Mathf.Abs(flatDist - MapController.Instance.CurrentRadius) <= range;
    }

    private Vector3 GetMistEntryDirection()
    {
        // Recht op de speler af; als die onbekend is, naar het midden van de map
        if (playerTarget != null)
        {
            Vector3 toPlayer = playerTarget.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f) return toPlayer.normalized;
        }
        if (MapController.Instance != null)
        {
            Vector3 toCenter = MapController.Instance.PlatformCenter - transform.position;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f) return toCenter.normalized;
        }
        return transform.forward;
    }

    // ── Movement ─────────────────────────────────────────────────────────────

    private void ApplyMovement(float dt)
    {
        if (state == BehaviourState.Inactive || state == BehaviourState.Dying)
            return;

        float distToPlayer = playerTarget != null
            ? Vector3.Distance(transform.position, playerTarget.position)
            : 0f;
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

        // Beam-evasion kans groeit dynamisch: geesten leren de zaklamp beter te ontwijken
        float dynamicEvasion = Mathf.Clamp01(beamEvasionChance + GetDynamicBonus() * 0.15f);
        if (isInFlashlightBeam
            && state != BehaviourState.Weakened
            && state != BehaviourState.Recoil
            && state != BehaviourState.Retreat
            && Random.value <= dynamicEvasion)
        {
            result += GetBeamEvasionVector(toPlayerFlat) * beamEvasionStrength;
        }

        return result;
    }

    private Vector3 GetPrimaryDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        switch (state)
        {
            case BehaviourState.MistEntry:  return GetMistEntryDirection();
            case BehaviourState.Chase:      return toPlayerFlat;
            case BehaviourState.Intercept:  return GetInterceptDirection(toPlayerFlat, distToPlayer);
            case BehaviourState.Flank:      return GetFlankDirection(toPlayerFlat, distToPlayer);
            case BehaviourState.Weakened:   return GetWeakenedDirection(toPlayerFlat, distToPlayer);
            case BehaviourState.Fleeing:    return fleeDirection;
            case BehaviourState.Retreat:    return retreatDirection;
            default:                        return toPlayerFlat;
        }
    }

    private Vector3 GetInterceptDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        Rigidbody playerRb = playerTarget.GetComponent<Rigidbody>();
        Vector3 playerVel = playerRb != null
            ? new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z)
            : Vector3.zero;

        float timeToReach = distToPlayer / Mathf.Max(moveSpeed * chaseSpeedMultiplier, 0.1f);
        // Slimmere interceptie naarmate de wave vordert: hogere lookahead bij hoge dynamicBonus
        float runtimeLookAhead = Mathf.Min(
            effectiveInterceptLookAhead * (1f + GetDynamicBonus() * 0.8f),
            interceptLookAheadMax * 1.5f);
        Vector3 toIntercept = playerTarget.position
            + playerVel * timeToReach * runtimeLookAhead
            - transform.position;
        toIntercept.y = 0f;
        return toIntercept.sqrMagnitude > 0.01f ? toIntercept.normalized : toPlayerFlat;
    }

    private Vector3 GetFlankDirection(Vector3 toPlayerFlat, float distToPlayer)
    {
        if (distToPlayer <= attackDistance) return toPlayerFlat;

        float effectiveRadius;
        if (_isLuring)
            // Lokgeest houdt een grote afstand om zichtbaar maar buiten direct gevaar te blijven
            effectiveRadius = lureOrbitRadius;
        else
            // Early waves orbit further out; scales toward flankOrbitRadius at high aggression
            effectiveRadius = Mathf.Lerp(flankOrbitRadiusEarlyWave, flankOrbitRadius, waveAggressionLevel);

        Vector3 orbitOffset = Quaternion.Euler(0f, flankAngle, 0f) * Vector3.forward * effectiveRadius;
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
        // MistEntry-geesten laden recht naar binnen: geen enkele boundary-push
        if (state == BehaviourState.MistEntry) return Vector3.zero;

        // Wegvliegende geesten mogen de muur wél passeren
        if (_isFlyingOut) return Vector3.zero;

        // Geesten die al door de muur zijn mogen er niet meer uit
        if (_hasPassedMistWall && MapController.Instance != null)
        {
            Vector3 mapCenter = MapController.Instance.PlatformCenter;
            float wallRadius = MapController.Instance.CurrentRadius - MapController.Instance.HardWallInset;
            Vector3 pos = transform.position;
            float flatDist = new Vector2(pos.x - mapCenter.x, pos.z - mapCenter.z).magnitude;
            if (flatDist >= wallRadius - mistWallKeepoutDistance)
            {
                Vector3 toCenter = new Vector3(mapCenter.x - pos.x, 0f, mapCenter.z - pos.z);
                return toCenter.normalized;
            }
        }

        // Bij een circulaire map: vijanden die buiten de rand spawnen mogen vrij
        // naar de speler toe vliegen. Boundary-check alleen voor vijanden die
        // al op het platform staan (binnen de map-radius).
        if (MapController.Instance != null)
        {
            Vector3 mapCenter = MapController.Instance.transform.position;
            float flatDist = new Vector2(
                transform.position.x - mapCenter.x,
                transform.position.z - mapCenter.z).magnitude;

            if (flatDist > MapController.Instance.CurrentRadius + 1f)
                return Vector3.zero;
        }

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
            if (!Physics.Raycast(origin, Vector3.down, heightRaycastDistance, groundLayers,
                QueryTriggerInteraction.Ignore))
                push -= dir;
        }
        return push.sqrMagnitude > 0.01f ? push.normalized : Vector3.zero;
    }

    // ── Speed & height ───────────────────────────────────────────────────────

    private float GetTargetSpeed(float distToPlayer)
    {
        if (state == BehaviourState.MistEntry)
            return Mathf.Min(moveSpeed, _baseSpeed * 1.2f); // cap entry speed ongeacht wave-multipliers

        if (state == BehaviourState.Weakened)
        {
            float speedFraction = 1f - flashlightEffectFactor * 0.9f;
            float baseWeakenedSpeed = weakenedSpeed * Mathf.Max(speedFraction, 0.1f);
            float spectrumMultiplier = Mathf.Lerp(0.4f, 2.0f, (aggressionSpectrum + 1f) * 0.5f);
            return baseWeakenedSpeed * spectrumMultiplier;
        }
        if (state == BehaviourState.Fleeing) return fleeSpeed;
        if (state == BehaviourState.Retreat) return retreatSpeed;

        float baseSpeed = moveSpeed * chaseSpeedMultiplier;
        // Runtime snelheidsbonus: geesten worden sneller naarmate meer kills + wave-voortgang
        float runtimeSpeedMult = 1f + GetDynamicBonus() * 0.35f;
        float speed = baseSpeed * GetDistanceBoost(distToPlayer) * GetFatigueMultiplier() * runtimeSpeedMult;

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

        float distToPlayer = playerTarget != null
            ? Vector3.Distance(transform.position, playerTarget.position)
            : 0f;
        float targetY = GetTargetFlyingHeight(distToPlayer);
        float diff = targetY - transform.position.y;

        if (state == BehaviourState.Weakened || isInFlashlightBeam)
        {
            float upSpeed = verticalSpeed * 0.55f;
            return Mathf.Clamp(diff * upSpeed, -verticalSpeed * 0.4f, upSpeed);
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
            return Mathf.Lerp(restHeight, surfaceY + maxFloatHeight * 0.65f, flashlightDamage);

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

        // Fallback: gebruik MapController.SurfaceY als er geen grond geraakt wordt
        // (bijv. boven de afgrond buiten de circulaire map).
        float groundFallback = MapController.Instance != null
            ? MapController.Instance.SurfaceY
            : 0f;

        float groundY = Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit,
            heightRaycastDistance, groundLayers, QueryTriggerInteraction.Ignore)
            ? groundHit.point.y
            : groundFallback;

        float obstacleY = Physics.Raycast(origin, Vector3.down, out RaycastHit obstacleHit,
            heightRaycastDistance, obstacleLayers, QueryTriggerInteraction.Ignore)
            ? obstacleHit.point.y : float.MinValue;

        return Mathf.Max(groundY, obstacleY);
    }

    // ── Animator ─────────────────────────────────────────────────────────────

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetInteger(HashState, (int)state);
        animator.SetFloat(HashSpeed, rb.linearVelocity.magnitude);
        animator.SetBool(HashIsMoving, rb.linearVelocity.sqrMagnitude > 0.1f);
        animator.SetBool(HashIsWeakened, state == BehaviourState.Weakened);
        animator.SetBool(HashIsDying, state == BehaviourState.Dying);
    }

    // ── Combat ───────────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        if (_materializationProgress < 0.75f) return;

        HealthController health = collision.gameObject.GetComponentInParent<HealthController>();
        if (health == null || !health.CompareTag("Player")) return;

        hasHit = true;
        WaveManager.Instance?.NotifyEnemyKilled(countForCombo: false);

        health.TakeDamage(health.MaxHealth * damagePercentage);

        PlayerController pc = health.GetComponent<PlayerController>();
        if (pc != null)
        {
            Vector3 dir = health.transform.position - transform.position;
            dir.y = 0f;
            pc.ApplyKnockback(dir, knockbackForce, upwardKnockbackForce);
        }

        Destroy(gameObject);
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, lungeTriggerDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, separationRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, coordAttackRadius);
    }
}