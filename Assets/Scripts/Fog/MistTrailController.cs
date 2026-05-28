using UnityEngine;
using System.Collections.Generic;

public class MistTrailController : MonoBehaviour
{
    public static MistTrailController Instance { get; private set; }

    [SerializeField] private int resolution = 256;
    [SerializeField] private float worldSize = 100f;
    [SerializeField] private float worldCenterX = 0f;
    [SerializeField] private float worldCenterZ = 0f;
    [SerializeField] private float recoverySpeed = 0.4f;
    [SerializeField] private float stampRadius = 3f;

    [Header("Swirl")]
    [SerializeField] private float swirlStrength = 2.0f;
    [SerializeField] private float swirlDecay = 0.94f;
    [SerializeField] private float swirlRadius = 4f;

    private static readonly List<Transform> _displacers = new List<Transform>();
    private Texture2D _trailTex;
    private float[] _trailData;
    private Texture2D _swirlTex;
    private float[] _swirlData; // RGFloat: pairs [vx, vz, vx, vz, ...]
    private readonly Dictionary<Transform, Vector3> _prevPositions = new Dictionary<Transform, Vector3>();

    public static void Register(Transform t)
    {
        if (!_displacers.Contains(t)) _displacers.Add(t);
    }

    public static void Unregister(Transform t) => _displacers.Remove(t);

    /// <summary>Schaal de world size van de mist mee met de map grootte.</summary>
    public void SetWorldSize(float size)
    {
        worldSize = Mathf.Max(10f, size);
    }

    private void Awake()
    {
        Instance = this;

        _trailTex = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);
        _trailTex.wrapMode = TextureWrapMode.Clamp;
        _trailTex.filterMode = FilterMode.Bilinear;
        _trailData = new float[resolution * resolution];
        for (int i = 0; i < _trailData.Length; i++) _trailData[i] = 1f;

        _swirlTex = new Texture2D(resolution, resolution, TextureFormat.RGFloat, false);
        _swirlTex.wrapMode = TextureWrapMode.Clamp;
        _swirlTex.filterMode = FilterMode.Bilinear;
        _swirlData = new float[resolution * resolution * 2]; // starts at zero (no swirl)
    }

    private void Update()
    {
        float recovery = recoverySpeed * Time.deltaTime;
        float pixelsPerUnit = resolution / worldSize;
        int stampPx = Mathf.CeilToInt(stampRadius * pixelsPerUnit);
        float originX = worldCenterX - worldSize * 0.5f;
        float originZ = worldCenterZ - worldSize * 0.5f;

        // ── Trail recovery ────────────────────────────────────────────────────
        for (int i = 0; i < _trailData.Length; i++)
            _trailData[i] = Mathf.Min(1f, _trailData[i] + recovery);

        // ── Trail stamp ───────────────────────────────────────────────────────
        for (int d = 0; d < _displacers.Count; d++)
        {
            if (_displacers[d] == null) { _displacers.RemoveAt(d--); continue; }

            int px = Mathf.RoundToInt((_displacers[d].position.x - originX) * pixelsPerUnit);
            int pz = Mathf.RoundToInt((_displacers[d].position.z - originZ) * pixelsPerUnit);

            for (int dy = -stampPx; dy <= stampPx; dy++)
                for (int dx = -stampPx; dx <= stampPx; dx++)
                {
                    int nx = px + dx, nz = pz + dy;
                    if (nx < 0 || nx >= resolution || nz < 0 || nz >= resolution) continue;

                    float worldDist = Mathf.Sqrt(dx * dx + dy * dy) / pixelsPerUnit;
                    if (worldDist > stampRadius) continue;

                    float inf = 1f - worldDist / stampRadius;
                    _trailData[nz * resolution + nx] = Mathf.Max(0f, _trailData[nz * resolution + nx] - inf * inf);
                }
        }

        _trailTex.SetPixelData(_trailData, 0);
        _trailTex.Apply(false);

        Shader.SetGlobalTexture("_MistTrailMap", _trailTex);
        Shader.SetGlobalFloat("_MistTrailWorldSize", worldSize);
        Shader.SetGlobalFloat("_MistTrailOriginX", originX);
        Shader.SetGlobalFloat("_MistTrailOriginZ", originZ);

        // ── Swirl decay ───────────────────────────────────────────────────────
        for (int i = 0; i < _swirlData.Length; i++)
            _swirlData[i] *= swirlDecay;

        // ── Swirl stamp ───────────────────────────────────────────────────────
        int swirlPx = Mathf.CeilToInt(swirlRadius * pixelsPerUnit);

        for (int d = 0; d < _displacers.Count; d++)
        {
            Transform t = _displacers[d];
            if (t == null) continue;

            Vector3 pos = t.position;
            if (!_prevPositions.TryGetValue(t, out Vector3 prevPos))
            {
                _prevPositions[t] = pos;
                continue;
            }
            _prevPositions[t] = pos;

            Vector2 delta2D = new Vector2(pos.x - prevPos.x, pos.z - prevPos.z);
            float speed = delta2D.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            if (speed < 0.5f) continue;

            Vector2 velNorm = delta2D.normalized;
            float pushMag = Mathf.Clamp01(speed * 0.08f) * swirlStrength;

            int px = Mathf.RoundToInt((pos.x - originX) * pixelsPerUnit);
            int pz = Mathf.RoundToInt((pos.z - originZ) * pixelsPerUnit);

            for (int dy = -swirlPx; dy <= swirlPx; dy++)
            {
                for (int dx = -swirlPx; dx <= swirlPx; dx++)
                {
                    int nx = px + dx, nz = pz + dy;
                    if (nx < 0 || nx >= resolution || nz < 0 || nz >= resolution) continue;

                    float worldDistX = dx / pixelsPerUnit;
                    float worldDistZ = dy / pixelsPerUnit;
                    float worldDist = Mathf.Sqrt(worldDistX * worldDistX + worldDistZ * worldDistZ);
                    if (worldDist > swirlRadius || worldDist < 0.01f) continue;

                    float falloff = (1f - worldDist / swirlRadius);
                    falloff *= falloff;

                    // Tangentiële richting: rotatie gebaseerd op welke kant van het pad
                    float side = worldDistX * velNorm.y - worldDistZ * velNorm.x;
                    float sign = side >= 0f ? 1f : -1f;
                    float tangX = -(worldDistZ / worldDist) * sign;
                    float tangZ =  (worldDistX / worldDist) * sign;

                    float inf = falloff * pushMag;
                    int idx = (nz * resolution + nx) * 2;
                    _swirlData[idx]     += tangX * inf;
                    _swirlData[idx + 1] += tangZ * inf;
                }
            }
        }

        // Clamp om extreme waarden te voorkomen
        for (int i = 0; i < _swirlData.Length; i++)
            _swirlData[i] = Mathf.Clamp(_swirlData[i], -6f, 6f);

        _swirlTex.SetPixelData(_swirlData, 0);
        _swirlTex.Apply(false);
        Shader.SetGlobalTexture("_MistSwirlMap", _swirlTex);
    }

    private void OnDestroy()
    {
        if (_trailTex != null) Destroy(_trailTex);
        if (_swirlTex != null) Destroy(_swirlTex);
    }
}
