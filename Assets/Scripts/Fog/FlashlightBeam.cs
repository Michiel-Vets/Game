using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Light))]
public class FlashlightBeam : MonoBehaviour
{
    [SerializeField] private Material beamMaterial;
    [SerializeField] private Color beamColor = new Color(1f, 0.9f, 0.7f, 0.12f);
    [SerializeField] private int segments = 32;
    [SerializeField] private float tipAlpha = 0.0f;
    [SerializeField] private float baseAlpha = 0.18f;

    private Light _light;
    private FlashlightController _controller;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _mat;
    private float _lastSpotAngle;
    private float _lastRange;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _controller = GetComponent<FlashlightController>();

        var child = new GameObject("BeamCone");
        child.transform.SetParent(transform);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        _meshFilter = child.AddComponent<MeshFilter>();
        _meshRenderer = child.AddComponent<MeshRenderer>();
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;

        if (beamMaterial != null)
        {
            _mat = new Material(beamMaterial);
            _meshRenderer.sharedMaterial = _mat;
        }

        RebuildMesh();
    }

    private void Update()
    {
        bool controllerOn = _controller != null ? _controller.IsOn : true;
        bool lightEnabled = _light != null && _light.enabled;
        _meshRenderer.enabled = controllerOn && lightEnabled;

        if (_light != null && (_light.spotAngle != _lastSpotAngle || _light.range != _lastRange))
            RebuildMesh();
    }

    private void RebuildMesh()
    {
        if (_light == null) return;

        _lastSpotAngle = _light.spotAngle;
        _lastRange = _light.range;

        float length = _light.range;
        float radius = Mathf.Tan(_light.spotAngle * 0.5f * Mathf.Deg2Rad) * length;

        _meshFilter.sharedMesh = BuildCone(length, radius, segments);
    }

    private Mesh BuildCone(float length, float radius, int segs)
    {
        int vertCount = 1 + segs + 1;
        var verts = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var colors = new Color[vertCount];

        Color tipColor = new Color(beamColor.r, beamColor.g, beamColor.b, tipAlpha);
        Color baseRimColor = new Color(beamColor.r, beamColor.g, beamColor.b, baseAlpha);
        Color baseCenterColor = new Color(beamColor.r, beamColor.g, beamColor.b, baseAlpha * 0.3f);

        verts[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0f);
        colors[0] = tipColor;

        for (int i = 0; i < segs; i++)
        {
            float a = i * Mathf.PI * 2f / segs;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, length);
            uvs[i + 1] = new Vector2((float)i / segs, 1f);
            colors[i + 1] = baseRimColor;
        }

        verts[segs + 1] = new Vector3(0f, 0f, length);
        uvs[segs + 1] = new Vector2(0.5f, 1f);
        colors[segs + 1] = baseCenterColor;

        var tris = new int[segs * 6];
        int idx = 0;
        for (int i = 0; i < segs; i++)
        {
            int next = (i + 1) % segs;
            tris[idx++] = 0;
            tris[idx++] = i + 1;
            tris[idx++] = next + 1;
            tris[idx++] = segs + 1;
            tris[idx++] = next + 1;
            tris[idx++] = i + 1;
        }

        var mesh = new Mesh { name = "BeamCone" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}