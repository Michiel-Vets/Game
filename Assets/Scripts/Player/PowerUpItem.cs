using System.Collections;
using UnityEngine;

public class PowerUpItem : MonoBehaviour
{
    [Header("Emission (orb)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color emissionColor = new Color(6f, 4f, 1f, 1f); // HDR warm-geel

    [Header("Point Light")]
    [SerializeField] private float lightRange     = 12f;
    [SerializeField] private float lightIntensity = 4f;
    [SerializeField] private Color lightColor     = new Color(1f, 0.88f, 0.45f);

    private void Start()
    {
        ApplyEmission();
        EnsurePointLight();
    }

    private void ApplyEmission()
    {
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            Material mat = rend.material; // maakt een instantie per object

            bool emissionApplied = false;

            // Pad 1 — URP Lit / Standard: zet emission keyword + kleur
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                emissionApplied = mat.IsKeywordEnabled("_EMISSION");
            }

            // Pad 2 — Fallback: vervang door een nieuw URP Unlit materiaal met HDR basiskleur.
            // Dit werkt altijd, ongeacht het originele shader-type.
            if (!emissionApplied)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit != null)
                {
                    var glowMat = new Material(unlit);
                    glowMat.SetColor("_BaseColor", emissionColor);
                    rend.material = glowMat;
                }
                else if (mat.HasProperty("_BaseColor"))
                {
                    // Laatste redmiddel: zet basiskleur zo helder mogelijk
                    mat.SetColor("_BaseColor", emissionColor);
                }
            }
        }
    }

    private void EnsurePointLight()
    {
        if (GetComponentInChildren<Light>() != null) return;

        var lg = new GameObject("PickupLight");
        lg.transform.SetParent(transform);
        lg.transform.localPosition = Vector3.zero;

        var l       = lg.AddComponent<Light>();
        l.type      = LightType.Point;
        l.range     = lightRange;
        l.intensity = lightIntensity;
        l.color     = lightColor;
    }

    /// <summary>Fade uit over <paramref name="duration"/> seconden en destroy daarna.</summary>
    public void FadeOutAndDestroy(float duration = 1f)
    {
        GetComponent<Collider>().enabled = false; // niet meer oppakbaar
        StartCoroutine(FadeCoroutine(duration));
    }

    private IEnumerator FadeCoroutine(float duration)
    {
        float elapsed = 0f;
        var renderers = GetComponentsInChildren<Renderer>();
        var lights    = GetComponentsInChildren<Light>();

        // Sla start-kleuren op
        var startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i].material;
            startColors[i] = mat.HasProperty("_BaseColor")
                ? mat.GetColor("_BaseColor")
                : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
        }
        float startLightIntensity = lights.Length > 0 ? lights[0].intensity : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = startColors[i];
                c.a = t;
                var mat = renderers[i].material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
            foreach (var l in lights)
                l.intensity = startLightIntensity * t;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PowerUpSpawner.Instance?.OnPowerUpCollected(gameObject);

        if (WaveManager.Instance != null && WaveManager.Instance.IsBreak)
            WaveManager.Instance.AddBreakTime(5f);

        Destroy(gameObject);
    }
}
