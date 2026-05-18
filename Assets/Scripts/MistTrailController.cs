using UnityEngine;
using System.Collections.Generic;

public class MistTrailController : MonoBehaviour
{
    [SerializeField] private int resolution = 256;
    [SerializeField] private float worldSize = 100f;
    [SerializeField] private float worldCenterX = 0f;
    [SerializeField] private float worldCenterZ = 0f;
    [SerializeField] private float recoverySpeed = 0.4f;
    [SerializeField] private float stampRadius = 3f;

    private static readonly List<Transform> _displacers = new List<Transform>();
    private Texture2D _trailTex;
    private float[] _trailData;

    public static void Register(Transform t)
    {
        if (!_displacers.Contains(t)) _displacers.Add(t);
    }

    public static void Unregister(Transform t) => _displacers.Remove(t);

    private void Awake()
    {
        _trailTex = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);
        _trailTex.wrapMode = TextureWrapMode.Clamp;
        _trailTex.filterMode = FilterMode.Bilinear;
        _trailData = new float[resolution * resolution];
        for (int i = 0; i < _trailData.Length; i++) _trailData[i] = 1f;
    }

    private void Update()
    {
        float recovery = recoverySpeed * Time.deltaTime;
        float pixelsPerUnit = resolution / worldSize;
        int stampPx = Mathf.CeilToInt(stampRadius * pixelsPerUnit);
        float originX = worldCenterX - worldSize * 0.5f;
        float originZ = worldCenterZ - worldSize * 0.5f;

        for (int i = 0; i < _trailData.Length; i++)
            _trailData[i] = Mathf.Min(1f, _trailData[i] + recovery);

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
    }

    private void OnDestroy()
    {
        if (_trailTex != null) Destroy(_trailTex);
    }
}