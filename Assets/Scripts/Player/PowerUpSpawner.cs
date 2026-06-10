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

    // Hint beams (wave-richting aanwijzers — één per spawn-groep)
    private readonly List<Vector3>    _hintBeamPositions = new List<Vector3>();
    private readonly List<GameObject> _hintObjects       = new List<GameObject>();

    private readonly List<GameObject> _activePowerUps = new List<GameObject>();
    private List<Vector3> _pendingHintPositions = new List<Vector3>();
    private int _collectedThisBreak;
    private int _spawnedThisBreak;
    private Transform _player;
    private EnemySpawner _enemySpawner;

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
    public void OnBreakStarted(IReadOnlyList<Vector3> hintPositions)
    {
        PlayerFinder.TryAssignIfNull(ref _player);

        foreach (var go in _activePowerUps)
            if (go != null) Destroy(go);
        _activePowerUps.Clear();

        foreach (var go in _hintObjects) if (go != null) Destroy(go);
        _hintObjects.Clear();
        _hintBeamPositions.Clear();

        _pendingHintPositions = new List<Vector3>(hintPositions ?? new List<Vector3>());

        _collectedThisBreak = 0;
        _spawnedThisBreak = powerUpsPerBreak;

        if (_enemySpawner == null) _enemySpawner = FindObjectOfType<EnemySpawner>();
        for (int i = 0; i < powerUpsPerBreak; i++)
            _enemySpawner?.SpawnPowerUpScout(powerUpPrefab);


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
            if (item != null) item.FadeOutAndDestroy(1f);
            else Destroy(go);
        }
        _activePowerUps.Clear();

        foreach (var go in _hintObjects) if (go != null) Destroy(go);
        _hintObjects.Clear();
        _hintBeamPositions.Clear();
        _pendingHintPositions.Clear();
        UpdateBeamShaderGlobals();
        WaveProgressUI.Instance?.HidePowerUpCount();
    }

    /// <summary>Aanroepen vanuit PowerUpDropReward nadat de pickup gespawnd is.</summary>
    public void RegisterPowerUp(GameObject go)
    {
        if (go == null) return;
        _activePowerUps.Add(go);
        UpdateBeamShaderGlobals();
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
            ShowWaveHints();
    }

    /// <summary>Registreert één hint-beam in de shader (aangeroepen door WaveHintDisplay.Awake).</summary>
    public void RegisterHintBeam(Vector3 worldPosition)
    {
        if (!_hintBeamPositions.Contains(worldPosition))
            _hintBeamPositions.Add(worldPosition);
        UpdateBeamShaderGlobals();
    }

    /// <summary>Verwijdert de hint-beam van dit specifieke object (aangeroepen door WaveHintDisplay.OnDestroy).</summary>
    public void UnregisterHintBeam(Vector3 worldPosition)
    {
        _hintBeamPositions.Remove(worldPosition);
        UpdateBeamShaderGlobals();
    }

    /// <summary>Backward-compat: verwijdert alle hint beams.</summary>
    public void ClearHintBeam()
    {
        _hintBeamPositions.Clear();
        UpdateBeamShaderGlobals();
    }

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
        foreach (var hintPos in _hintBeamPositions)
        {
            if (count >= 4) break;
            _beamPosArray[count] = new Vector4(hintPos.x, hintPos.y, hintPos.z, 0f);
            count++;
        }
        // Zet ongebruikte slots op nul zodat de GPU nooit stale posities leest
        for (int i = count; i < 4; i++)
            _beamPosArray[i] = Vector4.zero;

        Shader.SetGlobalFloat(_propBeamCount, count);
        Shader.SetGlobalVectorArray(_propBeamPositions, _beamPosArray); // altijd sturen, ook bij count=0
    }

    private void ShowWaveHints()
    {
        WaveManager.Instance?.OnAllPowerUpsCollected();
        if (waveHintPrefab == null || _pendingHintPositions.Count == 0) return;
        foreach (var pos in _pendingHintPositions)
        {
            if (pos == Vector3.zero) continue;
            _hintObjects.Add(Instantiate(waveHintPrefab, pos, Quaternion.identity));
        }
    }

    private void CleanupList()
    {
        for (int i = _activePowerUps.Count - 1; i >= 0; i--)
            if (_activePowerUps[i] == null) _activePowerUps.RemoveAt(i);
    }
}
