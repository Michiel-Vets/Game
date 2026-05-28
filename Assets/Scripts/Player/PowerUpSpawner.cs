using System.Collections.Generic;
using UnityEngine;

public class PowerUpSpawner : MonoBehaviour
{
    public static PowerUpSpawner Instance { get; private set; }

    [Header("Prefabs")]
    [Tooltip("Prefab voor het power-up item (met PowerUpItem script, trigger collider en beam light).")]
    [SerializeField] private GameObject powerUpPrefab;
    [Tooltip("Optioneel: prefab voor de wave-richting hint (verschijnt als alle power-ups gepakt zijn).")]
    [SerializeField] private GameObject waveHintPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int powerUpsPerBreak = 3;
    [Tooltip("Minimale afstand van de speler bij spawnen.")]
    [SerializeField] private float minDistFromPlayer = 8f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float spawnYOffset = 1f;

    [Header("Mist Beam (shader)")]
    [SerializeField] private float beamHalfAngle       = 25f;
    [SerializeField] private float beamRange            = 35f;
    [SerializeField] private Color beamColor            = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField] private float beamScatterStrength  = 5f;

    // Shader property IDs
    private static readonly int _propBeamCount     = Shader.PropertyToID("_PowerUpBeamCount");
    private static readonly int _propBeamPositions = Shader.PropertyToID("_PowerUpBeamPositions");
    private static readonly int _propBeamCosAngle  = Shader.PropertyToID("_PowerUpBeamCosAngle");
    private static readonly int _propBeamRange     = Shader.PropertyToID("_PowerUpBeamRange");
    private static readonly int _propBeamColor     = Shader.PropertyToID("_PowerUpBeamColor");
    private static readonly int _propBeamStrength  = Shader.PropertyToID("_PowerUpBeamStrength");

    private readonly Vector4[] _beamPosArray = new Vector4[4];

    // Hint beam (wave-richting aanwijzer)
    private bool    _hintBeamActive;
    private Vector3 _hintBeamPosition;

    private readonly List<GameObject> _activePowerUps = new List<GameObject>();
    private GameObject _hintObject;
    private int _collectedThisBreak;
    private int _spawnedThisBreak;
    private Transform _player;

    /// <summary>Fractie van power-ups gepakt (0 = geen, 1 = alle). Bepaalt map-groei.</summary>
    public float PickupFraction => _spawnedThisBreak > 0
        ? (float)_collectedThisBreak / _spawnedThisBreak
        : 1f;

    private void Awake()
    {
        Instance = this;
        for (int i = 0; i < 4; i++) _beamPosArray[i] = Vector4.zero;
        Shader.SetGlobalFloat(_propBeamCount, 0f);
        Shader.SetGlobalVectorArray(_propBeamPositions, _beamPosArray);
    }

    /// <summary>Aanroepen vanuit WaveManager.BeginBreak().</summary>
    public void OnBreakStarted(Vector3 hintPosition)
    {
        PlayerFinder.TryAssignIfNull(ref _player);

        foreach (var go in _activePowerUps)
            if (go != null) Destroy(go);
        _activePowerUps.Clear();

        if (_hintObject != null) { Destroy(_hintObject); _hintObject = null; }
        _hintBeamActive = false;

        _collectedThisBreak = 0;
        _spawnedThisBreak = 0;

        for (int i = 0; i < powerUpsPerBreak; i++)
            TrySpawnPowerUp();

        _hintPosition = hintPosition;

        // Stel beam-shader properties in en stuur posities
        Shader.SetGlobalFloat(_propBeamCosAngle, Mathf.Cos(beamHalfAngle * Mathf.Deg2Rad));
        Shader.SetGlobalFloat(_propBeamRange, beamRange);
        Shader.SetGlobalVector(_propBeamColor, new Vector4(beamColor.r, beamColor.g, beamColor.b, 1f));
        Shader.SetGlobalFloat(_propBeamStrength, beamScatterStrength);
        UpdateBeamShaderGlobals();

        WaveProgressUI.Instance?.ShowPowerUpCount(0, _spawnedThisBreak);
    }

    /// <summary>Aanroepen vanuit WaveManager.BeginWave().</summary>
    public void OnWaveStarted()
    {
        foreach (var go in _activePowerUps)
        {
            if (go == null) continue;
            var item = go.GetComponent<PowerUpItem>();
            if (item != null)
                item.FadeOutAndDestroy(1f);
            else
                Destroy(go);
        }
        _activePowerUps.Clear();

        if (_hintObject != null) { Destroy(_hintObject); _hintObject = null; }

        _hintBeamActive = false;
        UpdateBeamShaderGlobals(); // stuurt count=0 + lege array
        WaveProgressUI.Instance?.HidePowerUpCount();
    }

    /// <summary>Aanroepen vanuit PowerUpItem.OnTriggerEnter().</summary>
    public void OnPowerUpCollected(GameObject collectedObject)
    {
        _collectedThisBreak++;
        _activePowerUps.Remove(collectedObject); // direct verwijderen vóór Destroy zodat de shader meteen klopt
        CleanupList();
        UpdateBeamShaderGlobals();

        WaveProgressUI.Instance?.ShowPowerUpCount(_collectedThisBreak, _spawnedThisBreak);

        if (_collectedThisBreak >= _spawnedThisBreak && _spawnedThisBreak > 0)
            ShowWaveHint();
    }

    /// <summary>Registreert de wave-hint als extra beam in de shader (aangeroepen door WaveHintDisplay).</summary>
    public void RegisterHintBeam(Vector3 worldPosition)
    {
        _hintBeamActive   = true;
        _hintBeamPosition = worldPosition;
        UpdateBeamShaderGlobals();
    }

    /// <summary>Verwijdert de wave-hint beam uit de shader.</summary>
    public void ClearHintBeam()
    {
        _hintBeamActive = false;
        UpdateBeamShaderGlobals();
    }

    private Vector3 _hintPosition;

    private void UpdateBeamShaderGlobals()
    {
        int count = 0;
        for (int i = 0; i < _activePowerUps.Count && count < 4; i++)
        {
            if (_activePowerUps[i] == null) continue;
            Vector3 p = _activePowerUps[i].transform.position;
            _beamPosArray[count] = new Vector4(p.x, p.y, p.z, 0f);
            count++;
        }
        if (_hintBeamActive && count < 4)
        {
            _beamPosArray[count] = new Vector4(_hintBeamPosition.x, _hintBeamPosition.y, _hintBeamPosition.z, 0f);
            count++;
        }
        // Zet ongebruikte slots op nul zodat de GPU nooit stale posities leest
        for (int i = count; i < 4; i++)
            _beamPosArray[i] = Vector4.zero;

        Shader.SetGlobalFloat(_propBeamCount, count);
        Shader.SetGlobalVectorArray(_propBeamPositions, _beamPosArray); // altijd sturen, ook bij count=0
    }

    private void ShowWaveHint()
    {
        if (waveHintPrefab == null || _hintPosition == Vector3.zero) return;
        _hintObject = Instantiate(waveHintPrefab, _hintPosition, Quaternion.identity);
    }

    private void TrySpawnPowerUp()
    {
        if (powerUpPrefab == null) return;

        float mapRadius = MapController.Instance != null
            ? MapController.Instance.CurrentRadius - MapController.Instance.HardWallInset - 3f
            : 30f;
        mapRadius = Mathf.Max(mapRadius, 5f);

        Vector3 center = MapController.Instance != null
            ? new Vector3(MapController.Instance.PlatformCenter.x, 0f, MapController.Instance.PlatformCenter.z)
            : Vector3.zero;

        for (int attempt = 0; attempt < 15; attempt++)
        {
            // Uniforme ring-verdeling van 30% tot 90% van de map radius
            float r = Mathf.Sqrt(Random.Range(0.09f, 0.81f)) * mapRadius;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            if (_player != null && Vector3.Distance(candidate, _player.position) < minDistFromPlayer)
                continue;

            Vector3 rayOrigin = candidate + Vector3.up * raycastHeight;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                    raycastHeight * 2f, groundLayer, QueryTriggerInteraction.Ignore))
                continue;

            Vector3 spawnPos = hit.point + Vector3.up * spawnYOffset;
            var go = Instantiate(powerUpPrefab, spawnPos, Quaternion.identity);
            _activePowerUps.Add(go);
            _spawnedThisBreak++;
            return;
        }
    }

    private void CleanupList()
    {
        for (int i = _activePowerUps.Count - 1; i >= 0; i--)
            if (_activePowerUps[i] == null) _activePowerUps.RemoveAt(i);
    }
}
