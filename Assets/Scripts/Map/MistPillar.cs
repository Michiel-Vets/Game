using UnityEngine;

/// <summary>
/// Toont een gloeiende mist-zuil op de plek waar de volgende wave vandaan komt.
/// Verschijnt een instelbare tijd voor de wave en verdwijnt zodra de wave begint.
///
/// Gebruik: sleep dit script op een leeg GameObject in de scène en wijs het toe
/// aan het 'Mist Pillar' veld van WaveManager.
/// Optioneel: stel 'Pillar Prefab' in voor een eigen particle-effect prefab;
/// anders wordt een procedurele cilinder gebruikt als fallback.
/// </summary>
public class MistPillar : MonoBehaviour
{
    [Header("Custom Prefab (optioneel)")]
    [Tooltip("Sleep hier een prefab met particle system / VFX. Laat leeg om de procedurele cilinder te gebruiken.")]
    [SerializeField] private GameObject pillarPrefab;

    [Header("Procedurele Cilinder (fallback)")]
    [SerializeField] private float pillarHeight = 10f;
    [SerializeField] private float pillarRadius = 1.2f;
    [ColorUsage(true, true)]
    [SerializeField] private Color pillarEmissionColor = new Color(0f, 3f, 6f, 1f); // HDR blauw
    [SerializeField] private float lightRange     = 18f;
    [SerializeField] private float lightIntensity = 4f;
    [SerializeField] private Color lightColor     = new Color(0.1f, 0.5f, 1f);

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 2f;
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 30f;

    public bool IsActive { get; private set; }

    private GameObject _instance;
    private Renderer   _renderer;
    private Light      _light;
    private float      _fadeTimer;
    private bool       _fadingOut;

    // ── Publieke API ─────────────────────────────────────────────────────────

    public void TryShow(Vector3 worldPosition)
    {
        if (IsActive && !_fadingOut) return;
        IsActive   = true;
        _fadingOut = false;
        _fadeTimer = 0f;

        Vector3 groundPos = SnapToGround(worldPosition);

        if (_instance == null)
            BuildVisual(groundPos);
        else
            _instance.transform.position = groundPos;

        _instance.SetActive(true);
        ApplyAlpha(0f);
    }

    public void Hide()
    {
        if (!IsActive) return;
        _fadingOut = true;
        _fadeTimer = 0f;
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsActive || _instance == null) return;

        _fadeTimer += Time.deltaTime;

        if (_fadingOut)
        {
            float t = 1f - Mathf.Clamp01(_fadeTimer / Mathf.Max(fadeOutDuration, 0.01f));
            ApplyAlpha(t);
            if (_fadeTimer >= fadeOutDuration)
            {
                _instance.SetActive(false);
                IsActive = false;
            }
        }
        else
        {
            float t = Mathf.Clamp01(_fadeTimer / Mathf.Max(fadeInDuration, 0.01f));
            ApplyAlpha(t);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Vector3 SnapToGround(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, pos.y + raycastHeight, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                raycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point;
        return pos;
    }

    private void BuildVisual(Vector3 position)
    {
        if (pillarPrefab != null)
        {
            _instance = Instantiate(pillarPrefab, position, Quaternion.identity, transform);
            _renderer = _instance.GetComponentInChildren<Renderer>();
            _light    = _instance.GetComponentInChildren<Light>();
            return;
        }

        // ── Procedurele cilinder ─────────────────────────────────────────────
        _instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _instance.name = "MistPillarCylinder";
        _instance.transform.SetParent(transform);
        Destroy(_instance.GetComponent<CapsuleCollider>());

        float halfH = pillarHeight * 0.5f;
        _instance.transform.position   = position + Vector3.up * halfH;
        _instance.transform.localScale  = new Vector3(pillarRadius * 2f, halfH, pillarRadius * 2f);

        _renderer = _instance.GetComponent<Renderer>();
        if (_renderer != null)
        {
            var mat = new Material(_renderer.sharedMaterial);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.05f, 0.1f, 0.2f, 1f));
            _renderer.material = mat;
        }

        // Glow-licht
        var lg = new GameObject("PillarLight");
        lg.transform.SetParent(_instance.transform);
        lg.transform.localPosition = Vector3.zero;
        _light           = lg.AddComponent<Light>();
        _light.type      = LightType.Point;
        _light.range     = lightRange;
        _light.color     = lightColor;
        _light.intensity = 0f;
    }

    private void ApplyAlpha(float t)
    {
        if (_renderer != null && _renderer.material.HasProperty("_EmissionColor"))
        {
            _renderer.material.SetColor("_EmissionColor", pillarEmissionColor * t);
        }
        if (_light != null)
            _light.intensity = lightIntensity * t;
    }
}
