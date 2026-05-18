using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Noise3DGenerator : MonoBehaviour
{
    [SerializeField] private int resolution = 64;
    [SerializeField] private string savePath = "Assets/Textures/FogNoise3D.asset";

    [ContextMenu("Generate Noise Texture")]
    public void Generate()
    {
        var tex = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;

        Color[] colors = new Color[resolution * resolution * resolution];
        for (int z = 0; z < resolution; z++)
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float fx = (float)x / resolution;
                    float fy = (float)y / resolution;
                    float fz = (float)z / resolution;
                    colors[z * resolution * resolution + y * resolution + x] = new Color(
                        Perlin3D(fx * 4, fy * 4, fz * 4),
                        Perlin3D(fx * 8 + 1.7f, fy * 8 + 9.2f, fz * 8 + 2.3f),
                        Perlin3D(fx * 16 + 3.1f, fy * 16 + 4.8f, fz * 16 + 7.2f),
                        Perlin3D(fx * 32 + 5.3f, fy * 32 + 1.4f, fz * 32 + 8.9f)
                    );
                }

        tex.SetPixels(colors);
        tex.Apply();
#if UNITY_EDITOR
        AssetDatabase.CreateAsset(tex, savePath);
        AssetDatabase.SaveAssets();
        Debug.Log("3D Noise opgeslagen: " + savePath);
#endif
    }

    private float Perlin3D(float x, float y, float z)
    {
        return (Mathf.PerlinNoise(x, y) + Mathf.PerlinNoise(x, z) +
                Mathf.PerlinNoise(y, z) + Mathf.PerlinNoise(y, x) +
                Mathf.PerlinNoise(z, x) + Mathf.PerlinNoise(z, y)) / 6f;
    }
}