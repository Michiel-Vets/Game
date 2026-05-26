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
    [Tooltip("Groei in radius per wave (minRadius + wave * groei).")]
    [SerializeField] private float radiusGrowthPerWave = 12.5f;
    [Tooltip("Maximale krimp bij een volledig mislukte wave.")]
    [SerializeField] private float maxShrinkPerWave = 20f;
    [Tooltip("Snelheid waarmee het platform visueel groeit of krimpt (eenheden/sec).")]
    [SerializeField] private float growthSpeed = 10f;

    [Header("Fog")]
    [Tooltip("Extra marge boven mapRadius voor de fog-grens (0 = precies op de kaartrand).")]
    [SerializeField] private float fogRadiusMargin = 0f;

    [Header("Boundary Wall")]
    [Tooltip("Aantal onzichtbare muursegmenten rond de kaartrand.")]
    [SerializeField] private int wallSegments = 24;
    [Tooltip("Hoogte van de onzichtbare muur (hoog genoeg dat de speler er niet overheen kan springen).")]
    [SerializeField] private float wallHeight = 5f;
    [Tooltip("Dikte van elk muursegment.")]
    [SerializeField] private float wallThickness = 0.4f;
    [Tooltip("Offset van de muur ten opzichte van de kaartrand (0 = precies op de rand).")]
    [SerializeField] private float wallEdgeOffset = 0f;

    // ── Publieke properties ───────────────────────────────────────────────────

    /// <summary>Huidige (vloeiend geïnterpoleerde) radius van het platform.</summary>
    public float CurrentRadius { get; private set; }

    /// <summary>Y-positie van het bovenoppervlak van het cilinder-platform.</summary>
    public float SurfaceY { get; private set; }

    /// <summary>XZ-middelpunt van het platform (gebruikt door MapBoundary).</summary>
    public Vector3 PlatformCenter => platformTransform != null
        ? platformTransform.position
        : transform.position;

    // ── Privé ─────────────────────────────────────────────────────────────────

    private float      _targetRadius;
    private Transform[] _wallSegmentTransforms;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        Instance      = this;
        CurrentRadius = minRadius;
        _targetRadius = minRadius;
    }

    private void Start()
    {
        RefreshSurfaceY();
        CreateBoundaryWall();
        ApplyPlatformScale();   // roept UpdateBoundaryWall + RefreshSurfaceY aan
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
    /// Aanroepen vanuit WaveManager.BeginWave().
    /// missedFraction = 0  → wave gecleared, map groeit normaal.
    /// missedFraction = 1  → wave volledig gemist, map krimpt maximaal.
    /// </summary>
    public void UpdateForWave(int waveNumber, float missedFraction = 0f)
    {
        if (missedFraction > 0.02f)
        {
            float shrink  = maxShrinkPerWave * missedFraction;
            _targetRadius = Mathf.Max(minRadius, _targetRadius - shrink);
        }
        else
        {
            _targetRadius = Mathf.Clamp(
                minRadius + (waveNumber - 1) * radiusGrowthPerWave,
                minRadius,
                maxRadius);
        }
    }

    // ── Interne helpers ───────────────────────────────────────────────────────

    private void ApplyPlatformScale()
    {
        if (platformTransform != null)
        {
            float scaleValue = cylinderScaleFactor > 0f
                ? CurrentRadius / cylinderScaleFactor
                : CurrentRadius;

            Vector3 s = platformTransform.localScale;
            platformTransform.localScale = new Vector3(scaleValue, s.y, scaleValue);
        }

        RefreshSurfaceY();
        UpdateBoundaryWall();
    }

    private void RefreshSurfaceY()
    {
        if (platformTransform == null) { SurfaceY = 0f; return; }
        // Unity Cylinder: totale hoogte = localScale.y * 2 → bovenkant = positie.y + localScale.y
        SurfaceY = platformTransform.position.y + platformTransform.localScale.y;
    }

    private void PushFogGlobals()
    {
        // _MapRadius = exacte kaartrand → mist-muur geplaatst hierop
        Shader.SetGlobalFloat("_MapRadius", CurrentRadius);
        // _GroundRadius = fog-dekkingsgebied
        Shader.SetGlobalFloat("_GroundRadius", CurrentRadius + fogRadiusMargin);
    }

    // ── Boundary wall ─────────────────────────────────────────────────────────

    /// <summary>
    /// Maakt de onzichtbare muursegmenten aan. Eenmalig aangeroepen vanuit Start().
    /// </summary>
    private void CreateBoundaryWall()
    {
        GameObject wallRoot = new GameObject("BoundaryWall");
        wallRoot.transform.SetParent(transform);

        _wallSegmentTransforms = new Transform[wallSegments];

        for (int i = 0; i < wallSegments; i++)
        {
            GameObject seg = new GameObject($"WallSeg_{i}");
            seg.transform.SetParent(wallRoot.transform);
            seg.AddComponent<BoxCollider>();   // geen Renderer → onzichtbaar
            _wallSegmentTransforms[i] = seg.transform;
        }
    }

    /// <summary>
    /// Past de positie en grootte van alle muursegmenten aan op de huidige radius.
    /// Aangeroepen elke keer dat ApplyPlatformScale() wordt aangeroepen.
    /// </summary>
    private void UpdateBoundaryWall()
    {
        if (_wallSegmentTransforms == null) return;

        float wallRadius = CurrentRadius + wallEdgeOffset;

        // Breedte per segment = stuk van de omtrek + kleine overlap om gaten te voorkomen
        float segmentWidth = (2f * Mathf.PI * wallRadius / wallSegments) + 0.5f;

        // Midden van de muur verticaal: van SurfaceY tot SurfaceY + wallHeight
        float wallCenterY = SurfaceY + wallHeight * 0.5f;

        // Centrum van het platform in XZ
        float cx = platformTransform != null ? platformTransform.position.x : 0f;
        float cz = platformTransform != null ? platformTransform.position.z : 0f;

        for (int i = 0; i < wallSegments; i++)
        {
            float angle = i * (360f / wallSegments) * Mathf.Deg2Rad;

            // Positie op de cirkel
            _wallSegmentTransforms[i].position = new Vector3(
                cx + Mathf.Sin(angle) * wallRadius,
                wallCenterY,
                cz + Mathf.Cos(angle) * wallRadius);

            // Rotatie zodat het segment tangentiaal (langs de cirkel) staat
            _wallSegmentTransforms[i].rotation =
                Quaternion.Euler(0f, i * (360f / wallSegments), 0f);

            // Grootte: breedte langs de cirkel, hoogte omhoog, dikte naar buiten
            _wallSegmentTransforms[i].GetComponent<BoxCollider>().size =
                new Vector3(segmentWidth, wallHeight, wallThickness);
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        float r = Application.isPlaying ? CurrentRadius : minRadius;

        // Map-rand (cyaan)
        Gizmos.color = Color.cyan;
        DrawCircle(transform.position, r);

        // Vijanden spawn-ring (oranje)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        DrawCircle(transform.position, r + 30f);
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
