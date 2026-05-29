using UnityEngine;

/// <summary>
/// Maak een leeg prefab en voeg dit script toe.
/// Bij instantiatie bouwt het script een zichtbare lichtpilaar en registreert
/// de positie als mist-beam in de shader (via PowerUpSpawner).
/// </summary>
public class WaveHintDisplay : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private float lightIntensity  = 18f;
    [SerializeField] private float lightRange      = 60f;
    [SerializeField] private float lightSpotAngle  = 25f;
    [SerializeField] private Color lightColor      = new Color(1f, 0.6f, 0.1f, 1f);

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed      = 2f;
    [SerializeField] private float pulseAmplitude  = 0.25f;

    private Light _light;
    private float _baseIntensity;

    private void Awake()
    {
        // Maak upward spot light aan
        var lightGo = new GameObject("HintLight");
        lightGo.transform.SetParent(transform);
        lightGo.transform.localPosition = Vector3.zero;
        lightGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // omhoog

        _light               = lightGo.AddComponent<Light>();
        _light.type          = LightType.Spot;
        _light.color         = lightColor;
        _light.intensity     = lightIntensity;
        _light.range         = lightRange;
        _light.spotAngle     = lightSpotAngle;
        _baseIntensity       = lightIntensity;

        // Registreer in Awake (niet Start): Awake loopt synchroon tijdens Instantiate,
        // zodat de beam altijd voor OnWaveStarted() staat als ze op hetzelfde frame vallen.
        PowerUpSpawner.Instance?.RegisterHintBeam(transform.position);

        // Maak fog corridor aan van pijler naar midden
        Vector3 center = MapController.Instance != null
            ? MapController.Instance.PlatformCenter
            : Vector3.zero;
        var corridorGo = new GameObject("FogCorridor");
        corridorGo.transform.SetParent(transform);
        var corridor = corridorGo.AddComponent<FogCorridor>();
        corridor.Activate(transform.position, center);
    }

    private void Update()
    {
        // Zelfvernietiging zodra de wave begint — vangnet ongeacht aanroepondervolgorde
        if (WaveManager.Instance != null && !WaveManager.Instance.IsBreak)
        {
            Destroy(gameObject);
            return;
        }

        if (_light != null)
            _light.intensity = _baseIntensity * (1f + pulseAmplitude * Mathf.Sin(Time.time * pulseSpeed));
    }

    private void OnDestroy()
    {
        PowerUpSpawner.Instance?.ClearHintBeam();
    }
}
