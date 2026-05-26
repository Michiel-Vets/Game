using UnityEngine;

/// <summary>
/// Beheert de groeiende cilinder-map.
///
/// Setup in de Unity Editor:
///  1. Voeg dit script toe aan een leeg GameObject (bijv. "MapController").
///  2. Sleep je cilinder-platform naar het 'Platform Transform' veld.
///  3. Zorg dat de cilinder op de "Ground" laag staat zodat vijanden en pickups
///     hem herkennen.
///
/// Let op: Unity's ingebouwde Cylinder-primitive heeft bij localScale.x = 1 een
/// world-radius van 1 eenheid. Pas 'Cylinder Scale Factor' aan als je eigen model
/// een andere schaal gebruikt.
/// </summary>
public class MapController : MonoBehaviour
{
    public static MapController Instance { get; private set; }

    [Header("Platform")]
    [Tooltip("Sleep hier het cilinder-platform naartoe.")]
    [SerializeField] private Transform platformTransform;
    [Tooltip("Hoeveel world-radius geeft localScale.x = 1? Voor Unity's ingebouwde Cylinder = 1.")]
    [SerializeField] private float cylinderScaleFactor = 1f;

    [Header("Radius Scaling")]
    [Tooltip("Startradius van het platform (wave 1).")]
    [SerializeField] private float minRadius = 50f;
    [Tooltip("Maximale radius die het platform kan bereiken.")]
    [SerializeField] private float maxRadius = 300f;
    [Tooltip("Groei in radius per wave (50 + wave * groei).")]
    [SerializeField] private float radiusGrowthPerWave = 12.5f;
    [Tooltip("Snelheid waarmee het platform visueel groeit (eenheden/sec).")]
    [SerializeField] private float growthSpeed = 10f;

    [Header("Fog")]
    [Tooltip("Extra marge boven mapRadius voor de fog-grens.")]
    [SerializeField] private float fogRadiusMargin = 40f;

    // ── Publieke properties ───────────────────────────────────────────────────

    /// <summary>Huidige (vloeiend geïnterpoleerde) radius van het platform.</summary>
    public float CurrentRadius { get; private set; }

    /// <summary>Y-positie van het bovenoppervlak van het cilinder-platform.</summary>
    public float SurfaceY { get; private set; }

    // ── Privé ─────────────────────────────────────────────────────────────────

    private float _targetRadius;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        CurrentRadius  = minRadius;
        _targetRadius  = minRadius;
    }

    private void Start()
    {
        RefreshSurfaceY();
        ApplyPlatformScale();
        PushFogGlobals();
    }

    private void Update()
    {
        if (Mathf.Abs(CurrentRadius - _targetRadius) > 0.01f)
        {
            CurrentRadius = Mathf.MoveTowards(CurrentRadius, _targetRadius, growthSpeed * Time.deltaTime);
            ApplyPlatformScale();
            PushFogGlobals();
        }
    }

    // ── Publieke methoden ─────────────────────────────────────────────────────

    /// <summary>
    /// Aanroepen vanuit WaveManager.BeginWave() om de doelradius voor de komende wave in te stellen.
    /// </summary>
    public void UpdateForWave(int waveNumber)
    {
        _targetRadius = Mathf.Clamp(
            minRadius + (waveNumber - 1) * radiusGrowthPerWave,
            minRadius,
            maxRadius);
    }

    // ── Interne helpers ───────────────────────────────────────────────────────

    private void ApplyPlatformScale()
    {
        if (platformTransform == null) return;

        // localScale.x zodat world-radius == CurrentRadius
        float scaleValue = cylinderScaleFactor > 0f
            ? CurrentRadius / cylinderScaleFactor
            : CurrentRadius;

        Vector3 s = platformTransform.localScale;
        platformTransform.localScale = new Vector3(scaleValue, s.y, scaleValue);

        RefreshSurfaceY();
    }

    private void RefreshSurfaceY()
    {
        if (platformTransform == null) { SurfaceY = 0f; return; }

        // Unity Cylinder: totale hoogte = localScale.y * 2 → bovenkant = positie.y + localScale.y
        SurfaceY = platformTransform.position.y + platformTransform.localScale.y;
    }

    private void PushFogGlobals()
    {
        float fogRadius = CurrentRadius + fogRadiusMargin;

        // Fog shader _GroundRadius bepaalt tot hoe ver de mist zichtbaar is
        Shader.SetGlobalFloat("_GroundRadius", fogRadius);

        // MistTrail worldSize mee schalen
        MistTrailController.Instance?.SetWorldSize(fogRadius * 2f);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        float r = Application.isPlaying ? CurrentRadius : minRadius;

        // Map-rand (cyaan)
        Gizmos.color = Color.cyan;
        DrawCircle(transform.position, r);

        // Vijanden spawn-ring (oranje, 30m buiten map)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        DrawCircle(transform.position, r + 30f);

        // Fog-grens (geel, gestippeld)
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        DrawCircle(transform.position, r + fogRadiusMargin);
    }

    private static void DrawCircle(Vector3 center, float radius, int segments = 64)
    {
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i       * step * Mathf.Deg2Rad;
            float a1 = (i + 1) * step * Mathf.Deg2Rad;
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Sin(a0) * radius, 0f, Mathf.Cos(a0) * radius),
                center + new Vector3(Mathf.Sin(a1) * radius, 0f, Mathf.Cos(a1) * radius));
        }
    }
}
