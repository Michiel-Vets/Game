using UnityEngine;
using System.Collections.Generic;

public class FlashlightController : MonoBehaviour
{
    public enum FlashlightMode { Off, Weak, Medium, Strong }

    [Header("References")]
    [SerializeField] private BatteryController batteryController;

    [Header("Light")]
    [SerializeField] private Light flashlight;
    [SerializeField] private bool startsOn = true;

    [Header("Beam Settings (Medium / Base)")]
    [SerializeField] private float maxDistance = 25f;
    [SerializeField] private float hitRadius = 1.2f;

    [Header("Damage / Weaken")]
    [SerializeField] private bool damageEnemies = true;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Distance Falloff")]
    [SerializeField] private float fullEffectDistance = 5f;
    [SerializeField, Range(0f, 1f)] private float minEffectAtMaxDistance = 0.1f;

    [Header("Weak Mode  (spot-only, brede bundel)")]
    [SerializeField] private float weakRange = 15f;
    [SerializeField] private float weakSpotAngle = 55f;
    [SerializeField] private float weakHitRadius = 2.2f;
    [SerializeField] private float weakBatteryMultiplier = 0.333f;

    [Header("Strong Mode  (hogere damage, smalle bundel)")]
    [SerializeField] private float strongRange = 38f;
    [SerializeField] private float strongSpotAngle = 18f;
    [SerializeField] private float strongHitRadius = 0.7f;
    [SerializeField] private float strongBatteryMultiplier = 2.0f;
    [SerializeField] private float strongEffectMultiplier = 1.8f;

    [Header("Combo Boost")]
    [SerializeField] private float comboIntensityMultiplier = 1.6f;
    [SerializeField] private float comboSpotAngleMultiplier = 1.4f;

    public bool IsOn => _mode != FlashlightMode.Off;
    public FlashlightMode CurrentMode => _mode;

    private FlashlightMode _mode = FlashlightMode.Off;
    private float _baseLightRange;
    private float _baseSpotAngle;
    private float _baseIntensity;
    private HashSet<EnemyController> _litEnemies = new HashSet<EnemyController>();

    private static readonly int PropPos      = Shader.PropertyToID("_FlashlightWorldPos");
    private static readonly int PropDir      = Shader.PropertyToID("_FlashlightWorldDir");
    private static readonly int PropCosAngle = Shader.PropertyToID("_FlashlightCosHalfAngle");
    private static readonly int PropRange    = Shader.PropertyToID("_FlashlightRange");
    private static readonly int PropEnabled  = Shader.PropertyToID("_FlashlightEnabled");

    private void Awake()
    {
        if (flashlight == null)
            flashlight = GetComponent<Light>();
        _baseLightRange = flashlight != null ? flashlight.range : maxDistance;
        _baseSpotAngle  = flashlight != null ? flashlight.spotAngle : 30f;
        _baseIntensity  = flashlight != null ? flashlight.intensity : 1f;
    }

    private void Start()
    {
        _mode = startsOn ? FlashlightMode.Medium : FlashlightMode.Off;
        ApplyMode();
    }

    private void Update()
    {
        if (_mode != FlashlightMode.Off)
        {
            if (batteryController != null)
            {
                float drainMult = GetDrainMultiplier();
                if (ComboSystem.Instance != null && ComboSystem.Instance.IsComboActive)
                    drainMult *= ComboSystem.Instance.OverchargedDrainMultiplier;
                batteryController.DrainBattery(Time.deltaTime * drainMult);

                bool overused = ComboSystem.Instance != null && ComboSystem.Instance.IsOverused;
                if (!batteryController.HasBattery && !overused)
                {
                    SetMode(FlashlightMode.Off);
                    return;
                }
            }

            HandleBeam();
        }

        UpdateShaderGlobals();
    }

    /// <summary>Cyclet: Uit → Zwak → Medium → Sterk → Uit</summary>
    public void CycleMode()
    {
        bool hasBattery = batteryController == null || batteryController.HasBattery;
        FlashlightMode next = (FlashlightMode)(((int)_mode + 1) % 4);
        if (next != FlashlightMode.Off && !hasBattery)
            next = FlashlightMode.Off;
        SetMode(next);
    }

    public void SetComboBoosted(bool boosted)
    {
        if (flashlight != null)
        {
            flashlight.intensity  = _baseIntensity  * (boosted ? comboIntensityMultiplier : 1f);
            flashlight.spotAngle  = _baseSpotAngle  * (boosted ? comboSpotAngleMultiplier : 1f);
        }
    }

    // Backwards-compat alias (werd aangeroepen vanuit PlayerController)
    public void Toggle() => CycleMode();

    private void SetMode(FlashlightMode mode)
    {
        _mode = mode;
        ApplyMode();
    }

    private void ApplyMode()
    {
        bool on = _mode != FlashlightMode.Off;

        if (flashlight != null)
        {
            flashlight.enabled = on;
            if (on)
            {
                switch (_mode)
                {
                    case FlashlightMode.Weak:
                        flashlight.range     = weakRange;
                        flashlight.spotAngle = weakSpotAngle;
                        break;
                    case FlashlightMode.Medium:
                        flashlight.range     = _baseLightRange;
                        flashlight.spotAngle = _baseSpotAngle;
                        break;
                    case FlashlightMode.Strong:
                        flashlight.range     = strongRange;
                        flashlight.spotAngle = strongSpotAngle;
                        break;
                }
            }
        }

        if (!on)
        {
            foreach (var enemy in _litEnemies)
                enemy.SetTargetVisibility(0f);
            _litEnemies.Clear();
        }

        UpdateShaderGlobals();
    }

    private float GetDrainMultiplier()
    {
        switch (_mode)
        {
            case FlashlightMode.Weak:   return weakBatteryMultiplier;
            case FlashlightMode.Strong: return strongBatteryMultiplier;
            default:                    return 1f;
        }
    }

    private void UpdateShaderGlobals()
    {
        bool active = _mode != FlashlightMode.Off && flashlight != null;
        Shader.SetGlobalFloat(PropEnabled, active ? 1f : 0f);

        if (!active) return;

        float cosHalfAngle = Mathf.Cos(flashlight.spotAngle * 0.5f * Mathf.Deg2Rad);
        Shader.SetGlobalVector(PropPos,      transform.position);
        Shader.SetGlobalVector(PropDir,      transform.forward);
        Shader.SetGlobalFloat(PropCosAngle,  cosHalfAngle);
        Shader.SetGlobalFloat(PropRange,     flashlight.range);
    }

    private void HandleBeam()
    {
        Vector3 origin    = transform.position;
        Vector3 direction = transform.forward;

        bool  spotOnly       = _mode == FlashlightMode.Weak || !damageEnemies;
        float effectMult     = _mode == FlashlightMode.Strong ? strongEffectMultiplier : 1f;
        // Combo / overused versterken de zaklamp-damage
        if (ComboSystem.Instance != null)
        {
            effectMult *= ComboSystem.Instance.ComboDamageMultiplier;
            effectMult *= ComboSystem.Instance.OverusedDamageMultiplier;
        }
        float currentMaxDist = GetCurrentMaxDistance();
        float currentHitRad  = GetCurrentHitRadius();

        float effectiveDistance = currentMaxDist;
        if (obstacleLayers != 0 &&
            Physics.Raycast(origin, direction, out RaycastHit obstacleHit, currentMaxDist, obstacleLayers))
        {
            effectiveDistance = obstacleHit.distance;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            origin, currentHitRad, direction, effectiveDistance, enemyLayers);

        HashSet<EnemyController> litThisFrame = new HashSet<EnemyController>();
        float halfSpotAngle = flashlight != null ? flashlight.spotAngle * 0.5f : 30f;

        foreach (RaycastHit hit in hits)
        {
            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            float hitDistance     = Vector3.Distance(origin, hit.point);
            float distanceFraction = Mathf.Clamp01(hitDistance / currentMaxDist);
            float effectFactor = Mathf.Lerp(1f, minEffectAtMaxDistance,
                Mathf.InverseLerp(fullEffectDistance / currentMaxDist, 1f, distanceFraction));
            effectFactor *= effectMult;

            Vector3 dirToEnemy = (hit.collider.transform.position - origin).normalized;
            float   angle      = Vector3.Angle(direction, dirToEnemy);
            float   angleVis   = Mathf.Clamp01(1f - (angle / halfSpotAngle));
            float   visibility = angleVis * effectFactor;

            enemy.SetTargetVisibility(visibility);
            enemy.ReceiveFlashlightHit(effectFactor, direction, spotOnly);

            litThisFrame.Add(enemy);
        }

        foreach (var enemy in _litEnemies)
            if (!litThisFrame.Contains(enemy))
                enemy.SetTargetVisibility(0f);

        _litEnemies = litThisFrame;
    }

    private float GetCurrentMaxDistance()
    {
        switch (_mode)
        {
            case FlashlightMode.Weak:   return weakRange;
            case FlashlightMode.Strong: return strongRange;
            default:                    return maxDistance;
        }
    }

    private float GetCurrentHitRadius()
    {
        switch (_mode)
        {
            case FlashlightMode.Weak:   return weakHitRadius;
            case FlashlightMode.Strong: return strongHitRadius;
            default:                    return hitRadius;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
    }
}
