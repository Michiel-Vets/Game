using UnityEngine;

public class GhostClothSetup : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private float topRadius = 0.35f;
    [SerializeField] private float bottomRadius = 0.65f;
    [SerializeField] private float robeHeight = 1.8f;
    [SerializeField] private int columns = 14;
    [SerializeField] private int rows = 14;

    [Header("Cloth Physics")]
    [SerializeField] private float stretchingStiffness = 0.85f;
    [SerializeField] private float bendingStiffness = 0.3f;
    [SerializeField] private float damping = 0.05f;
    [SerializeField][Range(0.05f, 0.4f)] private float pinnedFraction = 0.18f;

    [Header("Ghostly Sway")]
    [SerializeField] private float swayStrength = 1.8f;
    [SerializeField] private float swaySpeed = 0.6f;
    [SerializeField] private float randomTurbulence = 0.4f;

    [Header("Visuals")]
    [SerializeField] private Material ghostMaterial;

    private Cloth _cloth;
    private float _phaseX;
    private float _phaseZ;

    void Start()
    {
        _phaseX = Random.Range(0f, Mathf.PI * 2f);
        _phaseZ = Random.Range(0f, Mathf.PI * 2f);

        HideExistingRenderers();
        BuildHead();
        BuildRobe();
    }

    void Update()
    {
        if (_cloth == null) return;

        float t = Time.time * swaySpeed;

        _cloth.externalAcceleration = new Vector3(
            Mathf.Sin(t * 1.0f + _phaseX) * swayStrength,
            Mathf.Sin(t * 0.4f) * swayStrength * 0.3f,
            Mathf.Sin(t * 0.7f + _phaseZ) * swayStrength
        );

        _cloth.randomAcceleration = Vector3.one * randomTurbulence;
    }

    void HideExistingRenderers()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
    }

    void BuildHead()
    {
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "GhostHead";
        head.transform.SetParent(transform);
        head.transform.localPosition = Vector3.zero;
        head.transform.localScale = Vector3.one * (topRadius * 2f);
        Destroy(head.GetComponent<SphereCollider>());
        if (ghostMaterial != null)
            head.GetComponent<MeshRenderer>().sharedMaterial = ghostMaterial;
    }

    void BuildRobe()
    {
        var robeGO = new GameObject("GhostRobe");
        robeGO.transform.SetParent(transform);
        robeGO.transform.localPosition = Vector3.zero;
        robeGO.transform.localRotation = Quaternion.identity;
        robeGO.transform.localScale = Vector3.one;

        var boneGO = new GameObject("RootBone");
        boneGO.transform.SetParent(robeGO.transform);
        boneGO.transform.localPosition = Vector3.zero;
        var bone = boneGO.transform;

        var mesh = BuildMesh();

        var weights = new BoneWeight[mesh.vertexCount];
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i].boneIndex0 = 0;
            weights[i].weight0 = 1f;
        }
        mesh.boneWeights = weights;
        mesh.bindposes = new[] { bone.worldToLocalMatrix * robeGO.transform.localToWorldMatrix };

        var smr = robeGO.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new[] { bone };
        smr.rootBone = bone;
        smr.sharedMesh = mesh;

        if (ghostMaterial != null)
        {
            var robeMat = new Material(ghostMaterial);
            robeMat.SetFloat("_Cull", 0f);
            smr.sharedMaterial = robeMat;
        }

        _cloth = robeGO.AddComponent<Cloth>();
        _cloth.stretchingStiffness = stretchingStiffness;
        _cloth.bendingStiffness = bendingStiffness;
        _cloth.damping = damping;
        _cloth.useGravity = true;

        ApplyConstraints(_cloth, mesh.vertices);
    }

    Mesh BuildMesh()
    {
        int vCols = columns + 1;
        int vRows = rows + 1;
        var verts = new Vector3[vCols * vRows];
        var uvs = new Vector2[vCols * vRows];

        for (int r = 0; r < vRows; r++)
        {
            float t = (float)r / rows;
            float y = Mathf.Lerp(-robeHeight, 0f, t);
            float rad = Mathf.Lerp(bottomRadius, topRadius, t);

            for (int c = 0; c < vCols; c++)
            {
                float angle = (float)c / columns * Mathf.PI * 2f;
                verts[r * vCols + c] = new Vector3(Mathf.Cos(angle) * rad, y, Mathf.Sin(angle) * rad);
                uvs[r * vCols + c] = new Vector2((float)c / columns, t);
            }
        }

        var tris = new int[columns * rows * 6];
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int v0 = r * (columns + 1) + c;
                int v1 = v0 + 1;
                int v2 = (r + 1) * (columns + 1) + c;
                int v3 = v2 + 1;
                tris[idx++] = v0; tris[idx++] = v2; tris[idx++] = v1;
                tris[idx++] = v1; tris[idx++] = v2; tris[idx++] = v3;
            }
        }

        var mesh = new Mesh { name = "GhostRobeMesh" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void ApplyConstraints(Cloth cloth, Vector3[] verts)
    {
        var coeff = new ClothSkinningCoefficient[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            float t = (verts[i].y + robeHeight) / robeHeight;
            if (t >= 1f - pinnedFraction)
            {
                coeff[i].maxDistance = 0f;
                coeff[i].collisionSphereDistance = 0f;
            }
            else
            {
                coeff[i].maxDistance = (1f - t) * 0.45f;
                coeff[i].collisionSphereDistance = float.MaxValue;
            }
        }
        cloth.coefficients = coeff;
    }
}