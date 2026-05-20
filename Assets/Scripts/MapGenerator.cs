using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// MapGenerator — volledig bijgewerkte versie met:
///   • Plateau met hoogtevariatie (gelaagde Perlin noise)
///   • Steile klif op plateauRadius
///   • Losse gras-GameObjects met wind-animatie
///   • Terrain textuur-slots: gras + klif (sleep eigen texturen in Inspector)
///   • Bomen correct op terrein-oppervlak (voet op y=0 in prefab)
///   • Mist gecentreerd op plateau
/// </summary>
[ExecuteAlways]
public class MapGenerator : MonoBehaviour
{
    // ── Terrain ───────────────────────────────────────────────────────────────

    [Header("Terrain")]
    [Tooltip("Breedte en lengte. Moet groter zijn dan 2x plateauRadius.")]
    public int terrainWidth = 800;
    public int terrainLength = 800;
    [Tooltip("Max hoogte van het terrain in Unity-eenheden.")]
    public int terrainHeight = 120;
    [Tooltip("Heightmap resolutie — moet 2^n+1 zijn (bijv. 513 of 1025).")]
    public int heightmapRes = 513;

    // ── Plateau ───────────────────────────────────────────────────────────────

    [Header("Plateau")]
    [Tooltip("Straal van het vlakke plateau in Unity-eenheden.")]
    public float plateauRadius = 150f;
    [Tooltip("Basishhoogte van het plateau in Unity-eenheden.")]
    public float plateauHeight = 60f;
    [Tooltip("Breedte van de klif. Gebruik 2-5 voor bijna verticaal.")]
    [Range(1f, 30f)]
    public float cliffWidth = 4f;
    [Tooltip("Sterkte van de hoogtevariatie bovenop het plateau.")]
    [Range(0f, 8f)]
    public float plateauBumpStrength = 3f;
    [Tooltip("Schaal van de grote golven op het plateau.")]
    [Range(0.001f, 0.02f)]
    public float plateauNoiseScale = 0.005f;
    [Tooltip("Hoogte van het laagland (afgrond).")]
    [Range(0f, 0.03f)]
    public float lowlandHeight = 0.001f;

    // ── Texturen ──────────────────────────────────────────────────────────────

    [Header("Texturen")]
    [Tooltip("Textuur voor het grasvlak bovenop het plateau.")]
    public Texture2D grassTexture;
    [Tooltip("Normal map voor het gras (optioneel).")]
    public Texture2D grassNormal;
    [Tooltip("Textuur voor de kliffen en steile hellingen.")]
    public Texture2D cliffTexture;
    [Tooltip("Normal map voor de klif (optioneel).")]
    public Texture2D cliffNormal;
    [Tooltip("Tiling van de gras-textuur (hoe vaak herhaald per terreinbreedte).")]
    [Range(1f, 100f)]
    public float grassTiling = 30f;
    [Tooltip("Tiling van de klif-textuur.")]
    [Range(1f, 100f)]
    public float cliffTiling = 15f;

    // ── Gras GameObjects ──────────────────────────────────────────────────────

    [Header("Gras (losse GameObjects)")]
    [Tooltip("Aantal gras-pollen dat gespawnd wordt.")]
    [Range(0, 5000)]
    public int grassCount = 2000;
    [Tooltip("Gras spawnt alleen op deze minimale hoogte-fractie.")]
    [Range(0f, 0.9f)]
    public float grassMinHeightFrac = 0.35f;
    [Tooltip("Gras spawnt alleen onder deze maximale hoogte-fractie.")]
    [Range(0.1f, 1f)]
    public float grassMaxHeightFrac = 0.95f;
    public Color grassColorA = new Color(0.15f, 0.40f, 0.08f);
    public Color grassColorB = new Color(0.50f, 0.70f, 0.18f);
    [Range(0.3f, 2f)]
    public float grassMinHeight = 0.5f;
    [Range(0.5f, 4f)]
    public float grassMaxHeight = 1.4f;
    [Range(0.1f, 1f)]
    public float grassMinWidth = 0.15f;
    [Range(0.2f, 2f)]
    public float grassMaxWidth = 0.45f;
    [Tooltip("Windsterkte — hoe ver de grassprietjes buigen.")]
    [Range(0f, 1f)]
    public float windStrength = 0.35f;
    [Tooltip("Windsnelheid.")]
    [Range(0.1f, 3f)]
    public float windSpeed = 0.8f;

    // ── Bomen ─────────────────────────────────────────────────────────────────

    [Header("Bomen")]
    [Tooltip("Eigen boom-prefab. Leeg = automatische capsule-boom.")]
    public GameObject treePrefab;
    [Range(10, 500)]
    public int treeCount = 60;
    [Range(0f, 0.9f)]
    public float treeMinFrac = 0.35f;
    [Range(0.1f, 1f)]
    public float treeMaxFrac = 0.90f;
    [Range(0.5f, 5f)]
    public float treeMinScale = 2f;
    [Range(1f, 10f)]
    public float treeMaxScale = 5f;

    // ── Verlichting ───────────────────────────────────────────────────────────

    [Header("Verlichting")]
    public Color sunColor = new Color(0.9f, 0.85f, 0.7f);
    [Range(0.1f, 3f)]
    public float sunIntensity = 1.0f;

    // ── Mist ──────────────────────────────────────────────────────────────────

    [Header("Mist")]
    [Tooltip("0 = gebruik plateau diameter automatisch.")]
    public float mistWorldSize = 0f;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _generated = false;
    private GameObject _grassParent;
    private Terrain _terrain;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (!_generated) Generate();
    }

    private void Update()
    {
        // Wind-animatie voor losse gras-objecten
        AnimateGrass();
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Generate Map")]
    public static void GenerateFromMenu()
    {
        MapGenerator gen = FindObjectOfType<MapGenerator>();
        if (gen == null)
            gen = new GameObject("MapGenerator").AddComponent<MapGenerator>();
        gen.Generate();
    }
#endif

    // ── Genereer ──────────────────────────────────────────────────────────────

    public void Generate()
    {
        _generated = true;

        RemoveGenerated();

        TerrainData data = BuildTerrainData();
        GameObject terrGO = CreateTerrainGameObject(data);
        _terrain = terrGO.GetComponent<Terrain>();

        ApplyTerrainTextures(data);
        SetupTerrain(_terrain);
        AddTrees(data);
        SpawnGrassObjects(data, terrGO.transform.position);
        EnsureLight();
        ConfigureMist();

        Debug.Log("[MapGenerator] Map gegenereerd!");

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
#endif
    }

    // ── Heightmap ─────────────────────────────────────────────────────────────

    private TerrainData BuildTerrainData()
    {
        TerrainData data = new TerrainData();
        data.heightmapResolution = heightmapRes;
        data.size = new Vector3(terrainWidth, terrainHeight, terrainLength);
        data.SetHeights(0, 0, BuildHeightmap());
        return data;
    }

    private float[,] BuildHeightmap()
    {
        int w = heightmapRes;
        float[,] h = new float[w, w];
        float halfW = terrainWidth / 2f;
        float halfL = terrainLength / 2f;
        float platFrac = plateauHeight / terrainHeight;
        float lowFrac = lowlandHeight;
        // Bump strength als fractie van terrainHeight
        float bumpFrac = plateauBumpStrength / terrainHeight;

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < w; z++)
            {
                float wx = Mathf.Lerp(-halfW, halfW, (float)x / (w - 1));
                float wz = Mathf.Lerp(-halfL, halfL, (float)z / (w - 1));
                float dist = Mathf.Sqrt(wx * wx + wz * wz);
                float edge = dist - plateauRadius;

                float heightFrac;

                if (edge <= 0f)
                {
                    // Plateau: basis + gelaagde Perlin voor zachte heuvels
                    float n1 = Mathf.PerlinNoise(wx * plateauNoiseScale + 3.7f,
                                                  wz * plateauNoiseScale + 1.3f);
                    float n2 = Mathf.PerlinNoise(wx * plateauNoiseScale * 2.5f + 7.1f,
                                                  wz * plateauNoiseScale * 2.5f + 4.9f) * 0.4f;
                    float n3 = Mathf.PerlinNoise(wx * plateauNoiseScale * 6f + 1.2f,
                                                  wz * plateauNoiseScale * 6f + 8.3f) * 0.15f;

                    // Zacht richting de rand laten oplopen (geeft een licht kussen-effect)
                    float edgeFade = Mathf.Clamp01(1f - dist / plateauRadius);
                    float rimBump = Mathf.Pow(edgeFade, 3f) * 0.3f * bumpFrac;

                    heightFrac = platFrac + (n1 + n2 + n3) * bumpFrac - rimBump;
                }
                else if (edge <= cliffWidth)
                {
                    // Klif: power-curve voor steile wand
                    float t = edge / cliffWidth;
                    float steep = t * t * t * (3f - 2f * t * t); // quintic smoothstep
                    heightFrac = Mathf.Lerp(platFrac, lowFrac, steep);
                }
                else
                {
                    heightFrac = lowFrac;
                }

                h[x, z] = Mathf.Clamp01(heightFrac);
            }
        }

        return h;
    }

    // ── Terrain GameObject ────────────────────────────────────────────────────

    private GameObject CreateTerrainGameObject(TerrainData data)
    {
        Vector3 origin = new Vector3(-terrainWidth / 2f, 0f, -terrainLength / 2f);
        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "GeneratedTerrain";
        go.tag = "Untagged";
        go.transform.position = origin;
        go.layer = 7; // Ground
        return go;
    }

    private void SetupTerrain(Terrain terrain)
    {
        terrain.drawInstanced = true;
        terrain.basemapDistance = 600f;
        terrain.detailObjectDistance = 150f;
        terrain.treeDistance = 500f;
        terrain.treeBillboardDistance = 100f;
    }

    // ── Texturen ──────────────────────────────────────────────────────────────

    private void ApplyTerrainTextures(TerrainData data)
    {
        // Bouw twee TerrainLayer-objecten: één voor gras, één voor klif
        var grassLayer = new TerrainLayer();
        grassLayer.name = "GrassLayer";
        grassLayer.diffuseTexture = grassTexture != null
            ? grassTexture
            : MakeSolidTexture(new Color(0.25f, 0.55f, 0.12f));
        grassLayer.normalMapTexture = grassNormal;
        grassLayer.tileSize = new Vector2(
            terrainWidth / grassTiling,
            terrainLength / grassTiling);

        var cliffLayer = new TerrainLayer();
        cliffLayer.name = "CliffLayer";
        cliffLayer.diffuseTexture = cliffTexture != null
            ? cliffTexture
            : MakeSolidTexture(new Color(0.45f, 0.38f, 0.28f));
        cliffLayer.normalMapTexture = cliffNormal;
        cliffLayer.tileSize = new Vector2(
            terrainWidth / cliffTiling,
            terrainLength / cliffTiling);

        data.terrainLayers = new[] { grassLayer, cliffLayer };

        // Splatmap: layer 0 = gras (plateau), layer 1 = klif (steile hellingen + rand)
        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;
        float halfW = terrainWidth / 2f;
        float halfL = terrainLength / 2f;

        float[,,] splatmap = new float[ah, aw, 2];

        for (int y = 0; y < ah; y++)
        {
            for (int x = 0; x < aw; x++)
            {
                // Normaliseerde positie (0-1)
                float nx = (float)x / (aw - 1);
                float ny = (float)y / (ah - 1);

                // Steepness van dit punt (0=vlak, 90=verticaal)
                float steepness = data.GetSteepness(nx, ny);
                // Wereld-afstand tot midden
                float wx = Mathf.Lerp(-halfW, halfW, nx);
                float wy = Mathf.Lerp(-halfL, halfL, ny);
                float dist = Mathf.Sqrt(wx * wx + wy * wy);

                // Klif-gewicht: hoog bij steile helling of dichtbij klif-rand
                float cliffBlend = Mathf.Clamp01(steepness / 35f);
                // Extra klif op de rand van het plateau
                float edgeDist = Mathf.Abs(dist - plateauRadius);
                float edgeBlend = Mathf.Clamp01(1f - edgeDist / (cliffWidth + 2f));
                cliffBlend = Mathf.Max(cliffBlend, edgeBlend);

                float grassBlend = 1f - cliffBlend;

                splatmap[y, x, 0] = grassBlend;
                splatmap[y, x, 1] = cliffBlend;
            }
        }

        data.SetAlphamaps(0, 0, splatmap);
    }

    private Texture2D MakeSolidTexture(Color col)
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = col;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ── Gras GameObjects met wind ─────────────────────────────────────────────

    private readonly List<GrassBlade> _blades = new List<GrassBlade>();

    private struct GrassBlade
    {
        public Transform transform;
        public float windOffset; // random phase
        public float height;
    }

    private void SpawnGrassObjects(TerrainData data, Vector3 terrainOrigin)
    {
        _blades.Clear();

        _grassParent = new GameObject("GrassParent");
        _grassParent.name = "GrassParent";

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");

        int hw = heightmapRes;
        int placed = 0;
        int attempts = 0;
        int maxAtt = grassCount * 10;

        while (placed < grassCount && attempts < maxAtt)
        {
            attempts++;

            float nx = Random.value;
            float nz = Random.value;

            float worldH = data.GetInterpolatedHeight(nx, nz);
            float hFrac = worldH / terrainHeight;

            if (hFrac < grassMinHeightFrac || hFrac > grassMaxHeightFrac) continue;

            // Alleen op vlak gedeelte (geen klif)
            float steepness = data.GetSteepness(nx, nz);
            if (steepness > 25f) continue;

            float worldX = terrainOrigin.x + nx * terrainWidth;
            float worldZ = terrainOrigin.z + nz * terrainLength;
            float worldY = terrainOrigin.y + worldH;

            float bladeH = Random.Range(grassMinHeight, grassMaxHeight);
            float bladeW = Random.Range(grassMinWidth, grassMaxWidth);

            // Een grasspriet = plat quad (Plane primitief gedraaid)
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blade.name = "Grass";
            blade.transform.SetParent(_grassParent.transform);

            // Voet van de spruit op de grond
            blade.transform.position = new Vector3(worldX, worldY, worldZ);
            blade.transform.localScale = new Vector3(bladeW, bladeH, 1f);
            blade.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // Pivot is midden van Quad → verschuif omhoog zodat voet op grond staat
            blade.transform.position += Vector3.up * (bladeH * 0.5f);

            // Verwijder collider (performance)
            DestroyImmediate(blade.GetComponent<MeshCollider>());

            // Kleur
            Color col = Color.Lerp(grassColorA, grassColorB, Random.value);
            Material mat = new Material(shader);
            mat.color = col;

            // Transparante rendering voor de onderkant
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 0); // Opaque
            }

            blade.GetComponent<Renderer>().sharedMaterial = mat;

            _blades.Add(new GrassBlade
            {
                transform = blade.transform,
                windOffset = Random.Range(0f, Mathf.PI * 2f),
                height = bladeH,
            });

            placed++;
        }

        Debug.Log($"[MapGenerator] {placed} gras-objecten gespawnd.");
    }

    private void AnimateGrass()
    {
        if (_blades == null || _blades.Count == 0) return;

        float t = Time.time * windSpeed;

        foreach (var blade in _blades)
        {
            if (blade.transform == null) continue;

            // Simpele sinus-wind: alleen de top buigt, voet blijft op grond
            float sway = Mathf.Sin(t + blade.windOffset) * windStrength;

            // Roteer rondom de voet (pivot is al op de grond dankzij de offset)
            Quaternion baseRot = blade.transform.rotation;
            // Stel tilt in op de lokale X-as (vooruit/achteruit buigen)
            Vector3 euler = baseRot.eulerAngles;
            blade.transform.rotation = Quaternion.Euler(
                sway * 15f,  // max 15 graden tilt
                euler.y,
                euler.z
            );
        }
    }

    // ── Bomen ─────────────────────────────────────────────────────────────────

    private void AddTrees(TerrainData data)
    {
        GameObject prefab = treePrefab != null ? treePrefab : BuildCapsuleTreePrefab();

        data.treePrototypes = new[]
        {
            new TreePrototype { prefab = prefab, bendFactor = 0.3f }
        };

        var instances = new TreeInstance[treeCount];
        int placed = 0;
        int attempts = 0;
        int maxAttempts = treeCount * 30;

        while (placed < treeCount && attempts < maxAttempts)
        {
            attempts++;
            float nx = Random.value;
            float nz = Random.value;

            float worldH = data.GetInterpolatedHeight(nx, nz);
            float hFrac = worldH / terrainHeight;
            float steepness = data.GetSteepness(nx, nz);

            // Bomen alleen op vlak plateau, niet op klif
            if (hFrac < treeMinFrac || hFrac > treeMaxFrac) continue;
            if (steepness > 20f) continue;

            float scale = Random.Range(treeMinScale, treeMaxScale);

            instances[placed] = new TreeInstance
            {
                // position.y = genormaliseerde hoogte zodat boom op terrein staat
                position = new Vector3(nx, worldH / terrainHeight, nz),
                widthScale = scale,
                heightScale = scale,
                rotation = Random.Range(0f, Mathf.PI * 2f),
                color = Color.white,
                lightmapColor = Color.white,
                prototypeIndex = 0,
            };
            placed++;
        }

        if (placed < treeCount) System.Array.Resize(ref instances, placed);
        data.treeInstances = instances;
        Debug.Log($"[MapGenerator] {placed} bomen geplaatst.");
    }

    private GameObject BuildCapsuleTreePrefab()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");

        // ── Stam ──
        GameObject trunk = new GameObject("TreePrefab_Generated");

        GameObject stemGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stemGO.transform.SetParent(trunk.transform);
        // Cylinder pivot zit in het midden → verschuif omhoog zodat voet op y=0 staat
        stemGO.transform.localPosition = new Vector3(0f, 1f, 0f); // hoogte 1 = halve cylinder
        stemGO.transform.localScale = new Vector3(0.18f, 1f, 0.18f);
        DestroyImmediate(stemGO.GetComponent<CapsuleCollider>());

        Material trunkMat = new Material(shader);
        trunkMat.color = new Color(0.30f, 0.18f, 0.06f);
        stemGO.GetComponent<Renderer>().sharedMaterial = trunkMat;

        // ── Bladeren ──
        GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.transform.SetParent(trunk.transform);
        leaves.transform.localPosition = new Vector3(0f, 2.8f, 0f);
        leaves.transform.localScale = new Vector3(2.2f, 1.8f, 2.2f);
        DestroyImmediate(leaves.GetComponent<SphereCollider>());

        Material leafMat = new Material(shader);
        leafMat.color = new Color(0.10f, 0.40f, 0.08f);
        leaves.GetComponent<Renderer>().sharedMaterial = leafMat;

        return trunk;
    }

    // ── Verlichting ───────────────────────────────────────────────────────────

    private void EnsureLight()
    {
        Light dirLight = null;
        foreach (Light l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { dirLight = l; break; }

        if (dirLight == null)
        {
            GameObject go = new GameObject("Directional Light (Generated)");
            dirLight = go.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        dirLight.color = sunColor;
        dirLight.intensity = sunIntensity;
        dirLight.shadows = LightShadows.Soft;
    }

    // ── Mist ──────────────────────────────────────────────────────────────────

    private void ConfigureMist()
    {
        MistTrailController mist = FindObjectOfType<MistTrailController>();
        if (mist == null) { Debug.Log("[MapGenerator] Geen MistTrailController gevonden."); return; }

        float wSize = mistWorldSize > 0f ? mistWorldSize : plateauRadius * 2f;

        var t = typeof(MistTrailController);
        var flags = System.Reflection.BindingFlags.Instance
                  | System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Public;

        SetField(mist, t, flags, "worldSize", wSize);
        SetField(mist, t, flags, "worldCenterX", 0f);
        SetField(mist, t, flags, "worldCenterZ", 0f);
        SetField(mist, t, flags, "stampRadius", 5f);

        Debug.Log($"[MapGenerator] Mist ingesteld. WorldSize = {wSize}");
    }

    private static void SetField(object obj, System.Type type,
        System.Reflection.BindingFlags flags, string name, object value)
    {
        var f = type.GetField(name, flags);
        if (f != null) f.SetValue(obj, value);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void RemoveGenerated()
    {
        // Terrain
        foreach (Terrain t in FindObjectsOfType<Terrain>())
            if (t.gameObject.name == "GeneratedTerrain")
                DestroyImmediate(t.gameObject);

        // Gras parent
        GameObject old = GameObject.Find("GrassParent");
        if (old != null) DestroyImmediate(old);

        _blades.Clear();
    }
}