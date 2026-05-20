using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// MapGenerator — volledig bijgewerkt met:
///   • 4 boom-soorten (Inspector-slots)
///   • 2 gras-soorten + 3 foliage-soorten (losse GameObjects met wind)
///   • 2 grote rotsen + 4 kleine rotsen (Inspector-slots)
///   • Zichtbare hoogtevariatie op plateau (genormaliseerde noise)
///   • Gras/foliage snap exact op terreinoppervlak via raycast
///   • Gras/foliage/kleine rotsen zonder colliders
///   • Overlap-preventie zodat objecten niet in elkaar spawnen
///   • Rotsen spawnen halverwege in de grond
///   • Mistmuur (MaxDistance / VisibilityDistance) instelbaar via Inspector
/// </summary>
[ExecuteAlways]
public class MapGenerator : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    // TERRAIN
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Terrain")]
    public int terrainWidth = 800;
    public int terrainLength = 800;
    public int terrainHeight = 120;
    public int heightmapRes = 513;

    // ═══════════════════════════════════════════════════════════════════════
    // PLATEAU
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Plateau")]
    public float plateauRadius = 150f;
    public float plateauHeight = 60f;
    [Range(1f, 30f)] public float cliffWidth = 4f;
    [Range(0f, 30f)] public float plateauBumpStrength = 15f;
    [Range(0.001f, 0.02f)] public float plateauNoiseScale = 0.005f;
    [Range(0f, 0.03f)] public float lowlandHeight = 0.001f;

    // ═══════════════════════════════════════════════════════════════════════
    // TEXTUREN
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Texturen — Gras")]
    public Texture2D grassTexture;
    public Texture2D grassNormal;
    public Texture2D grassTexture2;
    public Texture2D grassNormal2;
    public Texture2D grassMacroTexture;

    [Header("Texturen — Klif")]
    public Texture2D cliffTexture;
    public Texture2D cliffNormal;
    public Texture2D cliffTexture2;
    public Texture2D cliffNormal2;

    [Header("Texturen — Tiling")]
    [Range(1f, 150f)] public float grassTiling = 40f;
    [Range(1f, 50f)] public float grassTiling2 = 8f;
    [Range(0.5f, 10f)] public float macroTiling = 2f;
    [Range(1f, 100f)] public float cliffTiling = 20f;
    [Range(1f, 30f)] public float cliffTiling2 = 5f;

    // ═══════════════════════════════════════════════════════════════════════
    // BOMEN
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Bomen (4 soorten)")]
    public GameObject treePrefab1;
    public GameObject treePrefab2;
    public GameObject treePrefab3;
    public GameObject treePrefab4;

    [Range(0, 300)] public int treeCount = 120;
    [Range(0f, 1f)] public float treeMinHeightFrac = 0.35f;
    [Range(0f, 1f)] public float treeMaxHeightFrac = 0.98f;
    [Range(0f, 50f)] public float treeCliffOverhang = 15f;
    [Range(0.5f, 5f)] public float treeMinScale = 1.5f;
    [Range(1f, 10f)] public float treeMaxScale = 4f;
    [Range(0f, 1f)] public float treeClusterStrength = 0.4f;

    // ═══════════════════════════════════════════════════════════════════════
    // GRAS
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Gras soort 1")]
    public GameObject grassPrefab1;
    [Range(0, 6000)] public int grassCount1 = 2500;
    public Color grass1ColorA = new Color(0.15f, 0.40f, 0.08f);
    public Color grass1ColorB = new Color(0.45f, 0.65f, 0.15f);
    [Range(0.3f, 2f)] public float grass1MinH = 0.4f;
    [Range(0.5f, 4f)] public float grass1MaxH = 1.0f;
    [Range(0.1f, 1f)] public float grass1MinW = 0.12f;
    [Range(0.2f, 2f)] public float grass1MaxW = 0.35f;

    [Header("Gras soort 2")]
    public GameObject grassPrefab2;
    [Range(0, 4000)] public int grassCount2 = 1200;
    public Color grass2ColorA = new Color(0.30f, 0.55f, 0.12f);
    public Color grass2ColorB = new Color(0.60f, 0.75f, 0.25f);
    [Range(0.3f, 3f)] public float grass2MinH = 0.8f;
    [Range(0.5f, 5f)] public float grass2MaxH = 2.0f;
    [Range(0.1f, 1f)] public float grass2MinW = 0.20f;
    [Range(0.2f, 2f)] public float grass2MaxW = 0.55f;

    [Header("Foliage soort 1")]
    public GameObject foliagePrefab1;
    [Range(0, 2000)] public int foliageCount1 = 400;
    [Range(0.3f, 3f)] public float foliage1MinScale = 0.5f;
    [Range(0.5f, 5f)] public float foliage1MaxScale = 1.5f;

    [Header("Foliage soort 2")]
    public GameObject foliagePrefab2;
    [Range(0, 2000)] public int foliageCount2 = 300;
    [Range(0.3f, 3f)] public float foliage2MinScale = 0.3f;
    [Range(0.5f, 5f)] public float foliage2MaxScale = 1.0f;

    [Header("Foliage soort 3")]
    public GameObject foliagePrefab3;
    [Range(0, 1000)] public int foliageCount3 = 150;
    [Range(0.3f, 5f)] public float foliage3MinScale = 0.8f;
    [Range(0.5f, 8f)] public float foliage3MaxScale = 2.5f;

    [Header("Gras/Foliage — Spawn Zone")]
    [Range(0f, 0.9f)] public float grassMinHeightFrac = 0.30f;
    [Range(5f, 60f)] public float grassMaxSteepness = 20f;

    [Header("Wind")]
    [Range(0f, 1f)] public float windStrength = 0.30f;
    [Range(0.1f, 3f)] public float windSpeed = 0.8f;

    [Header("Distance Culling")]
    [Range(20f, 300f)] public float vegetationVisibleDistance = 80f;

    // ═══════════════════════════════════════════════════════════════════════
    // ROTSEN
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Grote Rotsen (2 soorten)")]
    public GameObject largeRock1;
    public GameObject largeRock2;
    [Range(0, 60)] public int largeRockCount = 18;
    [Range(1f, 10f)] public float largeRockMinScale = 2f;
    [Range(2f, 20f)] public float largeRockMaxScale = 6f;

    [Header("Kleine Rotsen (4 soorten)")]
    public GameObject smallRock1;
    public GameObject smallRock2;
    public GameObject smallRock3;
    public GameObject smallRock4;
    [Range(0, 200)] public int smallRockCount = 80;
    [Range(0.2f, 3f)] public float smallRockMinScale = 0.3f;
    [Range(0.5f, 5f)] public float smallRockMaxScale = 1.5f;

    [Header("Rotsen — Spawn Zone")]
    [Range(0f, 60f)] public float rockCliffOverhang = 25f;
    [Range(10f, 89f)] public float rockMaxSteepness = 55f;
    [Tooltip("Hoe ver de rots in de grond zakt, als fractie van zijn schaal. 0.5 = halverwege.")]
    [Range(0f, 1f)] public float rockEmbedDepth = 0.45f;

    // ═══════════════════════════════════════════════════════════════════════
    // VERLICHTING
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Verlichting")]
    public Color sunColor = new Color(0.9f, 0.85f, 0.7f);
    [Range(0.1f, 3f)] public float sunIntensity = 1.0f;

    // ═══════════════════════════════════════════════════════════════════════
    // MIST
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Mist")]
    [Tooltip("0 = gebruik plateau diameter automatisch voor de MistTrailController.")]
    public float mistWorldSize = 0f;

    [Tooltip("Sleep hier VolumetricFogMat in. MaxDistance bepaalt de zichtbare mistmuur rond de speler.")]
    public Material volumetricFogMaterial;

    [Tooltip("Hoe ver de mistmuur zichtbaar is rondom de speler (shader _MaxDistance).")]
    [Range(5f, 200f)] public float fogMaxDistance = 40f;

    [Tooltip("Afstand waarop de mist volledig ondoorzichtig wordt (_VisibilityDistance).")]
    [Range(5f, 200f)] public float fogVisibilityDistance = 40f;

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private bool _generated = false;
    private GameObject _vegParent;
    private Terrain _terrain;

    private struct WindObject
    {
        public Transform transform;
        public float windOffset;
        public float windAmount;
    }
    private readonly List<WindObject> _windObjects = new List<WindObject>();

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    private void Start()
    {
        if (!_generated) Generate();
    }

    private void Update()
    {
        AnimateWind();
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

    // ═══════════════════════════════════════════════════════════════════════
    // GENERATE
    // ═══════════════════════════════════════════════════════════════════════

    public void Generate()
    {
        _generated = true;
        RemoveGenerated();
        _objectBounds.Clear();
        _grassBounds.Clear();

        TerrainData data = BuildTerrainData();
        GameObject terrGO = CreateTerrainGameObject(data);
        _terrain = terrGO.GetComponent<Terrain>();
        Vector3 origin = terrGO.transform.position;

        ApplyTerrainTextures(data);
        SetupTerrain(_terrain);
        AddTerrainTrees(data, origin);

        _vegParent = new GameObject("Vegetation");
        _windObjects.Clear();

        SpawnGrass(data, origin, grassPrefab1, grassCount1,
                   grass1ColorA, grass1ColorB,
                   grass1MinH, grass1MaxH, grass1MinW, grass1MaxW, 1.0f);

        SpawnGrass(data, origin, grassPrefab2, grassCount2,
                   grass2ColorA, grass2ColorB,
                   grass2MinH, grass2MaxH, grass2MinW, grass2MaxW, 0.7f);

        SpawnFoliage(data, origin, foliagePrefab1, foliageCount1,
                     foliage1MinScale, foliage1MaxScale, 0.4f);
        SpawnFoliage(data, origin, foliagePrefab2, foliageCount2,
                     foliage2MinScale, foliage2MaxScale, 0.25f);
        SpawnFoliage(data, origin, foliagePrefab3, foliageCount3,
                     foliage3MinScale, foliage3MaxScale, 0.15f);

        // Grote rotsen: colliders behouden, geen overlap-check
        SpawnRocks(data, origin,
                   new[] { largeRock1, largeRock2 },
                   largeRockCount, largeRockMinScale, largeRockMaxScale,
                   rockCliffOverhang, rockMaxSteepness,
                   removeColliders: false, checkOverlap: false);

        // Kleine rotsen: geen colliders, wel overlap-check
        SpawnRocks(data, origin,
                   new[] { smallRock1, smallRock2, smallRock3, smallRock4 },
                   smallRockCount, smallRockMinScale, smallRockMaxScale,
                   rockCliffOverhang, rockMaxSteepness,
                   removeColliders: true, checkOverlap: true);

        EnsureLight();
        ConfigureMist();

        Debug.Log("[MapGenerator] Map gegenereerd!");

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HEIGHTMAP
    // ═══════════════════════════════════════════════════════════════════════

    private TerrainData BuildTerrainData()
    {
        var data = new TerrainData();
        data.heightmapResolution = heightmapRes;
        data.size = new Vector3(terrainWidth, terrainHeight, terrainLength);
        data.SetHeights(0, 0, BuildHeightmap());
        return data;
    }

    private float[,] BuildHeightmap()
    {
        int w = heightmapRes;
        var h = new float[w, w];
        float halfW = terrainWidth / 2f;
        float halfL = terrainLength / 2f;
        float platFrac = plateauHeight / terrainHeight;
        // bumpFrac: plateauBumpStrength in world-units → fractie van terrainHeight.
        // Octaven wegen op tot ~1, zodat bumpStrength direct de max variatie in world-units is.
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
                    // Octaven gewogen zodat som ~[0, 1] is
                    float n1 = Mathf.PerlinNoise(wx * plateauNoiseScale + 3.7f,
                                                   wz * plateauNoiseScale + 1.3f) * 0.55f;
                    float n2 = Mathf.PerlinNoise(wx * plateauNoiseScale * 2.5f + 7.1f,
                                                   wz * plateauNoiseScale * 2.5f + 4.9f) * 0.28f;
                    float n3 = Mathf.PerlinNoise(wx * plateauNoiseScale * 6f + 1.2f,
                                                   wz * plateauNoiseScale * 6f + 8.3f) * 0.12f;
                    float micro = Mathf.PerlinNoise(wx * plateauNoiseScale * 14f,
                                                    wz * plateauNoiseScale * 14f) * 0.05f;

                    float noiseNorm = n1 + n2 + n3 + micro; // ~[0, 1]

                    float rimFade = Mathf.Clamp01(1f - dist / plateauRadius);
                    float rimBump = Mathf.Pow(rimFade, 3f) * 0.08f * bumpFrac;

                    heightFrac = platFrac + noiseNorm * bumpFrac + rimBump;
                }
                else if (edge <= cliffWidth)
                {
                    float t = edge / cliffWidth;
                    float steep = Mathf.Pow(t, 0.35f);
                    heightFrac = Mathf.Lerp(platFrac, lowlandHeight, steep);

                    float cliffNoise = Mathf.PerlinNoise(wx * 0.02f, wz * 0.02f) * 0.03f;
                    heightFrac += cliffNoise * (1f - t);
                }
                else
                {
                    heightFrac = lowlandHeight;
                }

                h[x, z] = Mathf.Clamp01(heightFrac);
            }
        }

        return h;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TERRAIN SETUP
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject CreateTerrainGameObject(TerrainData data)
    {
        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "GeneratedTerrain";
        go.transform.position = new Vector3(-terrainWidth / 2f, 0f, -terrainLength / 2f);
        go.layer = 7;
        return go;
    }

    private void SetupTerrain(Terrain terrain)
    {
        terrain.drawInstanced = true;
        terrain.basemapDistance = 1200f;
        terrain.detailObjectDistance = 40f;
        terrain.treeDistance = 40f;
        terrain.treeBillboardDistance = 120f;
        terrain.heightmapPixelError = 1f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEXTUREN (5-laags splatmap)
    // ═══════════════════════════════════════════════════════════════════════

    private void ApplyTerrainTextures(TerrainData data)
    {
        float tw = terrainWidth, tl = terrainLength;

        TerrainLayer MakeLayer(string n, Texture2D diff, Texture2D norm,
                               float tilingW, float tilingL)
        {
            var l = new TerrainLayer();
            l.name = n;
            l.diffuseTexture = diff ?? MakeSolidTexture(Color.gray);
            l.normalMapTexture = norm;
            l.tileSize = new Vector2(tilingW, tilingL);
            return l;
        }

        data.terrainLayers = new[]
        {
            MakeLayer("Grass_Fine",   grassTexture,  grassNormal,
                      tw / grassTiling,  tl / grassTiling),
            MakeLayer("Cliff_Fine",   cliffTexture,  cliffNormal,
                      tw / cliffTiling,  tl / cliffTiling),
            MakeLayer("Grass_Coarse", grassTexture2 ?? grassTexture,
                                      grassNormal2  ?? grassNormal,
                      tw / grassTiling2, tl / grassTiling2),
            MakeLayer("Cliff_Coarse", cliffTexture2 ?? cliffTexture,
                                      cliffNormal2  ?? cliffNormal,
                      tw / cliffTiling2, tl / cliffTiling2),
            MakeLayer("Grass_Macro",  grassMacroTexture ?? grassTexture, null,
                      tw / macroTiling,  tl / macroTiling),
        };

        int aw = data.alphamapWidth, ah = data.alphamapHeight;
        float halfW = terrainWidth / 2f, halfL = terrainLength / 2f;
        var splatmap = new float[ah, aw, 5];

        for (int y = 0; y < ah; y++)
        {
            for (int x = 0; x < aw; x++)
            {
                float nx = (float)x / (aw - 1);
                float ny = (float)y / (ah - 1);

                float steepness = data.GetSteepness(nx, ny);
                float wx = Mathf.Lerp(-halfW, halfW, nx);
                float wy = Mathf.Lerp(-halfL, halfL, ny);
                float dist = Mathf.Sqrt(wx * wx + wy * wy);

                float cliffW = Mathf.Clamp01(steepness / 30f);
                float edgeDist = Mathf.Abs(dist - plateauRadius);
                cliffW = Mathf.Max(cliffW,
                          Mathf.Clamp01(1f - edgeDist / (cliffWidth + 2f)));
                float grassW = 1f - cliffW;

                float macro = Mathf.Clamp01(
                    (Mathf.PerlinNoise(nx * macroTiling * 1.3f + 5f,
                                       ny * macroTiling * 1.3f + 2f) - 0.4f) * 1.8f)
                    * grassW * 0.45f;
                float grass2W = grassW * 0.30f;
                float cliff2W = cliffW * 0.35f;
                float grass1W = Mathf.Max(0f, grassW - grass2W - macro);
                float cliff1W = Mathf.Max(0f, cliffW - cliff2W);

                float total = grass1W + cliff1W + grass2W + cliff2W + macro;
                if (total > 0.001f)
                {
                    grass1W /= total; cliff1W /= total;
                    grass2W /= total; cliff2W /= total;
                    macro /= total;
                }
                else grass1W = 1f;

                splatmap[y, x, 0] = grass1W;
                splatmap[y, x, 1] = cliff1W;
                splatmap[y, x, 2] = grass2W;
                splatmap[y, x, 3] = cliff2W;
                splatmap[y, x, 4] = macro;
            }
        }

        data.SetAlphamaps(0, 0, splatmap);
    }

    private Texture2D MakeSolidTexture(Color col)
    {
        var tex = new Texture2D(4, 4);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = col;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOMEN via Terrain Tree systeem
    // ═══════════════════════════════════════════════════════════════════════

    private void AddTerrainTrees(TerrainData data, Vector3 origin)
    {
        var prefabs = new List<GameObject>();
        foreach (var p in new[] { treePrefab1, treePrefab2, treePrefab3, treePrefab4 })
            if (p != null) prefabs.Add(p);

        if (prefabs.Count == 0)
            prefabs.Add(BuildCapsuleTree());

        var protos = new TreePrototype[prefabs.Count];
        for (int i = 0; i < prefabs.Count; i++)
            protos[i] = new TreePrototype { prefab = prefabs[i], bendFactor = 0.3f };
        data.treePrototypes = protos;

        var instances = new List<TreeInstance>();
        int attempts = 0;
        int maxAttempts = treeCount * 30;

        while (instances.Count < treeCount && attempts < maxAttempts)
        {
            attempts++;
            float nx = Random.value;
            float nz = Random.value;

            Vector3 samplePos = new Vector3(
                origin.x + nx * terrainWidth,
                0f,
                origin.z + nz * terrainLength);

            float worldH = _terrain.SampleHeight(samplePos);
            float hFrac = worldH / terrainHeight;
            float steepness = _terrain.terrainData.GetSteepness(nx, nz);

            float wx = Mathf.Lerp(-terrainWidth / 2f, terrainWidth / 2f, nx);
            float wz = Mathf.Lerp(-terrainLength / 2f, terrainLength / 2f, nz);
            float dist = Mathf.Sqrt(wx * wx + wz * wz);

            bool onPlateau = dist <= plateauRadius && steepness < 25f;
            bool onCliffEdge = dist > plateauRadius
                            && dist <= plateauRadius + treeCliffOverhang
                            && steepness < 50f;

            if (!onPlateau && !onCliffEdge) continue;
            if (hFrac < treeMinHeightFrac || hFrac > treeMaxHeightFrac) continue;

            if (treeClusterStrength > 0f)
            {
                float cluster = Mathf.PerlinNoise(nx * 8f + 1f, nz * 8f + 1f);
                if (cluster < treeClusterStrength * 0.5f) continue;
            }

            float scale = Random.Range(treeMinScale, treeMaxScale);
            int protoIndex = Random.Range(0, prefabs.Count);

            instances.Add(new TreeInstance
            {
                position = new Vector3(nx, worldH / terrainHeight, nz),
                widthScale = scale,
                heightScale = scale,
                rotation = Random.Range(0f, Mathf.PI * 2f),
                color = Color.white,
                lightmapColor = Color.white,
                prototypeIndex = protoIndex,
            });
        }

        data.treeInstances = instances.ToArray();
        Debug.Log($"[MapGenerator] {instances.Count} bomen geplaatst ({prefabs.Count} soorten).");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRAS — losse GameObjects
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnGrass(TerrainData data, Vector3 origin,
                            GameObject prefab, int count,
                            Color colA, Color colB,
                            float minH, float maxH,
                            float minW, float maxW,
                            float windAmount)
    {


        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");

        int placed = 0, attempts = 0, maxAtt = count * 8;

        while (placed < count && attempts < maxAtt)
        {
            attempts++;
            float nx = Random.value, nz = Random.value;

            float worldH = data.GetInterpolatedHeight(nx, nz);
            float hFrac = worldH / terrainHeight;
            float steepness = data.GetSteepness(nx, nz);

            if (hFrac < grassMinHeightFrac) continue;
            if (steepness > grassMaxSteepness) continue;

            float wx = Mathf.Lerp(-terrainWidth / 2f, terrainWidth / 2f, nx);
            float wz = Mathf.Lerp(-terrainLength / 2f, terrainLength / 2f, nz);
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            if (dist > plateauRadius + cliffWidth * 0.3f) continue;

            float worldX = origin.x + nx * terrainWidth;
            float worldZ = origin.z + nz * terrainLength;

            // SampleHeight garandeert dat de Y overeenkomt met het zichtbare terrein
            float worldY = origin.y + _terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));

            float bladeH = Random.Range(minH, maxH);
            float bladeW = Random.Range(minW, maxW);

            float checkRadius = bladeW * 0.5f;
            Vector3 checkPos = new Vector3(worldX, worldY, worldZ);
            if (!OverlapsFree(checkPos, checkRadius, true)) continue;

            if (prefab == null)
                continue;

            GameObject blade = Instantiate(prefab);

            blade.layer = LayerMask.NameToLayer("Vegetation");

            DistanceCullingObject culling =
                blade.AddComponent<DistanceCullingObject>();

            typeof(DistanceCullingObject)
                .GetField("visibleDistance",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
                ?.SetValue(culling, vegetationVisibleDistance);

            blade.transform.localScale =
                new Vector3(bladeW, bladeH, bladeW);

            // Colliders verwijderen — zet eerst uit (instant effect),
            // daarna vernietigen (Destroy is veilig in Play mode)
            RemoveAllColliders(blade);

            blade.name = "Grass";
            blade.transform.SetParent(_vegParent.transform);
            blade.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // Quad-pivot zit in het midden → halve hoogte omhoog.
            // Prefab-pivot hoort onderaan te staan → direct op terrein.
            float yOffset = (prefab == null) ? bladeH * 0.5f : 0f;
            blade.transform.position = new Vector3(worldX, worldY + yOffset, worldZ);

            // Raycast-vangnet: snap altijd exact op het terreinoppervlak
            if (Physics.Raycast(new Vector3(worldX, worldY + 10f, worldZ),
                                Vector3.down, out RaycastHit hit, 30f, 1 << 7))
            {
                blade.transform.position = new Vector3(worldX,
                                                       hit.point.y + yOffset,
                                                       worldZ);
            }

            _grassBounds.Add((checkPos, checkRadius));
            _windObjects.Add(new WindObject
            {
                transform = blade.transform,
                windOffset = Random.Range(0f, Mathf.PI * 2f),
                windAmount = windAmount,
            });
            placed++;
        }

        Debug.Log($"[MapGenerator] {placed} gras-objecten gespawnd.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FOLIAGE — losse GameObjects
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnFoliage(TerrainData data, Vector3 origin,
                              GameObject prefab, int count,
                              float minScale, float maxScale,
                              float windAmount)
    {
        if (prefab == null)
        {
            Debug.Log("[MapGenerator] Foliage prefab niet ingesteld — overgeslagen.");
            return;
        }

        int placed = 0, attempts = 0, maxAtt = count * 10;

        while (placed < count && attempts < maxAtt)
        {
            attempts++;
            float nx = Random.value, nz = Random.value;

            float worldH = data.GetInterpolatedHeight(nx, nz);
            float hFrac = worldH / terrainHeight;
            float steepness = data.GetSteepness(nx, nz);

            if (hFrac < grassMinHeightFrac) continue;
            if (steepness > grassMaxSteepness) continue;

            float wx = Mathf.Lerp(-terrainWidth / 2f, terrainWidth / 2f, nx);
            float wz = Mathf.Lerp(-terrainLength / 2f, terrainLength / 2f, nz);
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            if (dist > plateauRadius) continue;

            float worldX = origin.x + nx * terrainWidth;
            float worldZ = origin.z + nz * terrainLength;
            float worldY = origin.y + _terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));

            float scale = Random.Range(minScale, maxScale);

            float checkRadius = scale * 0.5f;
            Vector3 checkPos = new Vector3(worldX, worldY, worldZ);
            if (!OverlapsFree(checkPos, checkRadius, false)) continue;

            var go = Instantiate(prefab);
            int vegetationLayer = LayerMask.NameToLayer("Vegetation");

            if (vegetationLayer != -1)
                go.layer = vegetationLayer;

            DistanceCullingObject culling =
                go.AddComponent<DistanceCullingObject>();

            culling.GetType()
                .GetField("visibleDistance",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
                ?.SetValue(culling, vegetationVisibleDistance);

            RemoveAllColliders(go);

            go.name = "Foliage";
            go.transform.SetParent(_vegParent.transform);
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.position = new Vector3(worldX, worldY, worldZ);

            // Raycast-vangnet
            if (Physics.Raycast(new Vector3(worldX, worldY + 10f, worldZ),
                                Vector3.down, out RaycastHit hit, 30f, 1 << 7))
            {
                go.transform.position = new Vector3(worldX, hit.point.y, worldZ);
            }

            _objectBounds.Add((checkPos, checkRadius));

            if (windAmount > 0f)
                _windObjects.Add(new WindObject
                {
                    transform = go.transform,
                    windOffset = Random.Range(0f, Mathf.PI * 2f),
                    windAmount = windAmount,
                });

            placed++;
        }

        Debug.Log($"[MapGenerator] {placed} foliage-objecten gespawnd.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROTSEN
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnRocks(TerrainData data, Vector3 origin,
                            GameObject[] prefabs, int count,
                            float minScale, float maxScale,
                            float cliffOverhang, float maxSteepness,
                            bool removeColliders = false,
                            bool checkOverlap = false)
    {
        var validPrefabs = new List<GameObject>();
        foreach (var p in prefabs)
            if (p != null) validPrefabs.Add(p);

        if (validPrefabs.Count == 0)
        {
            Debug.Log("[MapGenerator] Geen rots-prefabs ingesteld — overgeslagen.");
            return;
        }

        int placed = 0, attempts = 0, maxAtt = count * 15;

        while (placed < count && attempts < maxAtt)
        {
            attempts++;
            float nx = Random.value, nz = Random.value;

            float worldH = data.GetInterpolatedHeight(nx, nz);
            float steepness = data.GetSteepness(nx, nz);

            if (steepness > maxSteepness) continue;

            float wx = Mathf.Lerp(-terrainWidth / 2f, terrainWidth / 2f, nx);
            float wz = Mathf.Lerp(-terrainLength / 2f, terrainLength / 2f, nz);
            float dist = Mathf.Sqrt(wx * wx + wz * wz);

            bool onPlateau = dist <= plateauRadius;
            bool onCliffEdge = dist > plateauRadius
                            && dist <= plateauRadius + cliffOverhang;
            if (!onPlateau && !onCliffEdge) continue;
            if (onCliffEdge && steepness < 15f) continue;

            float worldX = origin.x + nx * terrainWidth;
            float worldZ = origin.z + nz * terrainLength;
            float worldY = origin.y + _terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));

            float scale = Random.Range(minScale, maxScale);

            if (checkOverlap)
            {
                float checkRadius = scale * 0.6f;
                Vector3 checkPos = new Vector3(worldX, worldY, worldZ);
                if (!OverlapsFree(checkPos, checkRadius, false)) continue;
                _objectBounds.Add((checkPos, checkRadius));
            }

            var go = Instantiate(validPrefabs[Random.Range(0, validPrefabs.Count)]);

            int vegetationLayer = LayerMask.NameToLayer("Vegetation");

            if (vegetationLayer != -1)
                go.layer = vegetationLayer;
            DistanceCullingObject culling =
                go.AddComponent<DistanceCullingObject>();

            culling.GetType()
                .GetField("visibleDistance",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
                ?.SetValue(culling, vegetationVisibleDistance);

            if (removeColliders)
                RemoveAllColliders(go);

            go.name = "Rock";
            go.transform.SetParent(_vegParent.transform);
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(
                Random.Range(-15f, 15f),
                Random.Range(0f, 360f),
                Random.Range(-15f, 15f));

            // Halverwege in de grond laten zakken
            float embedOffset = scale * rockEmbedDepth;
            go.transform.position = new Vector3(worldX, worldY - embedOffset, worldZ);

            placed++;
        }

        Debug.Log($"[MapGenerator] {placed} rotsen gespawnd.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WIND ANIMATIE
    // ═══════════════════════════════════════════════════════════════════════

    private void AnimateWind()
    {
        if (_windObjects == null || _windObjects.Count == 0) return;
        float t = Time.time * windSpeed;

        foreach (var obj in _windObjects)
        {
            if (obj.transform == null) continue;
            float sway = Mathf.Sin(t + obj.windOffset)
                        * windStrength * obj.windAmount;
            float sway2 = Mathf.Sin(t * 1.3f + obj.windOffset + 1f)
                        * windStrength * obj.windAmount * 0.4f;
            Vector3 euler = obj.transform.rotation.eulerAngles;
            obj.transform.rotation = Quaternion.Euler(
                sway * 12f,
                euler.y,
                sway2 * 8f);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FALLBACK BOOM
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject BuildCapsuleTree()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
        var root = new GameObject("TreePrefab_Generated");

        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.transform.SetParent(root.transform);
        stem.transform.localPosition = new Vector3(0f, 1f, 0f);
        stem.transform.localScale = new Vector3(0.18f, 1f, 0.18f);
        DestroyImmediate(stem.GetComponent<CapsuleCollider>());
        stem.GetComponent<Renderer>().sharedMaterial =
            new Material(shader) { color = new Color(0.28f, 0.16f, 0.05f) };

        var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.transform.SetParent(root.transform);
        leaves.transform.localPosition = new Vector3(0f, 2.8f, 0f);
        leaves.transform.localScale = new Vector3(2.2f, 1.8f, 2.2f);
        DestroyImmediate(leaves.GetComponent<SphereCollider>());
        leaves.GetComponent<Renderer>().sharedMaterial =
            new Material(shader) { color = new Color(0.10f, 0.38f, 0.07f) };

        return root;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VERLICHTING
    // ═══════════════════════════════════════════════════════════════════════

    private void EnsureLight()
    {
        Light dir = null;
        foreach (var l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { dir = l; break; }

        if (dir == null)
        {
            var go = new GameObject("Directional Light (Generated)");
            dir = go.AddComponent<Light>();
            dir.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(35f, -120f, 0f);
        }

        dir.color = sunColor;
        dir.intensity = sunIntensity;
        dir.shadows = LightShadows.Soft;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MIST
    // ═══════════════════════════════════════════════════════════════════════

    private void ConfigureMist()
    {
        // ── MistTrailController (displacement texture) ────────────────────
        var mist = FindObjectOfType<MistTrailController>();
        if (mist != null)
        {
            float wSize = mistWorldSize > 0f ? mistWorldSize : plateauRadius * 2f;
            var t = typeof(MistTrailController);
            var flags = System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Public;

            void Set(string name, object val)
            { var f = t.GetField(name, flags); if (f != null) f.SetValue(mist, val); }

            Set("worldSize", wSize);
            Set("worldCenterX", 0f);
            Set("worldCenterZ", 0f);
            Set("stampRadius", 5f);

            Debug.Log($"[MapGenerator] MistTrailController ingesteld. WorldSize = {wSize}");
        }

        // ── VolumetricFogMat: mistmuur rondom de speler ───────────────────
        // _MaxDistance en _VisibilityDistance bepalen hoe ver de mist
        // zichtbaar is vanaf de speler — dit is de "mistmuur".
        if (volumetricFogMaterial != null)
        {
            volumetricFogMaterial.SetFloat("_MaxDistance", fogMaxDistance);
            volumetricFogMaterial.SetFloat("_VisibilityDistance", fogVisibilityDistance);
            Debug.Log($"[MapGenerator] Mistmuur ingesteld: MaxDistance = {fogMaxDistance}, " +
                      $"VisibilityDistance = {fogVisibilityDistance}");
        }
        else
        {
            Debug.LogWarning("[MapGenerator] Geen VolumetricFogMat ingesteld — mistmuur niet bijgewerkt.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLEANUP
    // ═══════════════════════════════════════════════════════════════════════

    private void RemoveGenerated()
    {
        foreach (var t in FindObjectsOfType<Terrain>())
            if (t.gameObject.name == "GeneratedTerrain")
                DestroyImmediate(t.gameObject);

        var old = GameObject.Find("Vegetation");
        if (old != null) DestroyImmediate(old);

        _windObjects.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COLLIDERS VERWIJDEREN
    // ═══════════════════════════════════════════════════════════════════════

    private static void RemoveAllColliders(GameObject go)
    {
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
        {
            // Zet onmiddellijk uit zodat er nooit een frame is waarop de
            // collider nog actief is, ook als Destroy asynchroon loopt.
            col.enabled = false;
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // OVERLAP-CHECK
    // ═══════════════════════════════════════════════════════════════════════

    private readonly List<(Vector3 pos, float radius)> _objectBounds
    = new List<(Vector3, float)>();

    private readonly List<(Vector3 pos, float radius)> _grassBounds
        = new List<(Vector3, float)>();

    private bool OverlapsFree(Vector3 worldPos, float radius, bool isGrass = false)
    {
        var list = isGrass ? _grassBounds : _objectBounds;

        foreach (var (pos, r) in list)
            if (Vector3.Distance(worldPos, pos) < r + radius)
                return false;

        return true;
    }
}