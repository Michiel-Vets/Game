using UnityEngine;

public class GhostClothSetup : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private float topRadius = 0.35f;
    [SerializeField] private float shoulderRadius = 0.45f;
    [SerializeField] private float hemRadius = 0.3f;
    [SerializeField] private float robeHeight = 1.8f;
    [SerializeField] private float hoodExtension = 0.35f;
    [SerializeField] private int columns = 24;
    [SerializeField] private int rows = 18;

    [Header("Ridges")]
    [SerializeField] private int ridgeCount = 6;
    [SerializeField][Range(0f, 0.5f)] private float ridgeDepth = 0.2f;

    [Header("Inner Body")]
    [SerializeField] private float coneTopRadius = 0.8f;
    [SerializeField] private float coneTopY = -0.5f;
    [SerializeField] private float coneBottomRadius = 3.5f;
    [SerializeField] private float coneBottomY = -6f;
    [SerializeField] private int coneSegments = 5;

    [Header("Cloth Physics")]
    [SerializeField] private float stretchingStiffness = 0.35f;
    [SerializeField] private float bendingStiffness = 0.08f;
    [SerializeField] private float damping = 0.03f;
    [SerializeField][Range(0.05f, 0.4f)] private float pinnedFraction = 0.13f;
    [SerializeField] private float maxClothDistance = 0.45f;
    [SerializeField][Range(0.01f, 1f)] private float clothTimeScale = 0.15f;

    [Header("Ghostly Sway")]
    [SerializeField] private float swayStrength = 0.5f;
    [SerializeField] private float swaySpeed = 0.35f;
    [SerializeField] private float randomTurbulence = 1.5f;

    [Header("Movement Drag")]
    [SerializeField] private float dragStrength = 3f;
    [SerializeField] private float tiltStrength = 4f;
    [SerializeField] private float maxTiltAngle = 35f;
    [SerializeField] private float tiltSmoothing = 3f;

    [Header("Hover")]
    [SerializeField] private float hoverAmplitude = 0.4f;
    [SerializeField] private float hoverSpeed = 0.25f;
    [SerializeField] private float hoverRotationStrength = 8f;
    [SerializeField] private float hoverRotationSpeed = 0.2f;

    [Header("Visuals")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private bool showHead = false;

    private Cloth _cloth;
    private SkinnedMeshRenderer _smr;
    private Color _baseColor;
    private Rigidbody _rb;
    private Transform _visualRoot;

    private float _phaseX;
    private float _phaseZ;
    private float _hoverSeedY;
    private float _hoverSeedRot;

    private Vector3 _hoverOffset;
    private Quaternion _hoverRotation = Quaternion.identity;
    private Quaternion _movementTilt = Quaternion.identity;
    private Vector3 _dragForce;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();

        _phaseX = Random.Range(0f, Mathf.PI * 2f);
        _phaseZ = Random.Range(0f, Mathf.PI * 2f);
        _hoverSeedY = Random.Range(0f, 100f);
        _hoverSeedRot = Random.Range(0f, 100f);

        _visualRoot = new GameObject("GhostVisual").transform;
        _visualRoot.SetParent(transform);
        _visualRoot.localPosition = Vector3.zero;
        _visualRoot.localRotation = Quaternion.identity;
        _visualRoot.localScale = Vector3.one;

        HideExistingRenderers();
        if (showHead) BuildHead();
        BuildRobe();
    }

    void Update()
    {
        UpdateMovementDrag();
        UpdateClothSway();
        UpdateHover();
        ApplyVisualTransform();
        EnforceBounds();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void NotifyScaleChanged()
    {
        _cloth?.ClearTransformMotion();
    }
    public void SetVisibility(float alpha)
    {
        if (_smr == null) return;
        Color c = _baseColor;
        c.a = alpha;
        if (_smr.material.HasProperty("_BaseColor"))
            _smr.material.SetColor("_BaseColor", c);
        else
            _smr.material.SetColor("_Color", c);
    }

    // ── Drag & tilt ──────────────────────────────────────────────────────────

    void UpdateMovementDrag()
    {
        if (_rb == null) return;

        Vector3 worldVel = _rb.linearVelocity;
        Vector3 localVel = transform.InverseTransformDirection(worldVel);
        localVel.y = 0f;
        float speed = localVel.magnitude;

        if (speed > 0.05f)
        {
            localVel.Normalize();
            _dragForce = -localVel * speed * dragStrength * clothTimeScale;

            float angle = Mathf.Clamp(speed * tiltStrength, 0f, maxTiltAngle);
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, localVel);
            _movementTilt = Quaternion.Slerp(
                _movementTilt,
                Quaternion.AngleAxis(angle, tiltAxis),
                tiltSmoothing * Time.deltaTime
            );
        }
        else
        {
            _dragForce = Vector3.Lerp(_dragForce, Vector3.zero, tiltSmoothing * Time.deltaTime);
            _movementTilt = Quaternion.Slerp(_movementTilt, Quaternion.identity, tiltSmoothing * Time.deltaTime);
        }
    }

    void UpdateClothSway()
    {
        if (_cloth == null) return;

        float worldScale = transform.localScale.x;
        float t = Time.time * swaySpeed;
        float scale = clothTimeScale * worldScale;

        Vector3 gravity = Physics.gravity * clothTimeScale;
        Vector3 worldDrag = transform.TransformDirection(_dragForce);

        _cloth.externalAcceleration = gravity + worldDrag + new Vector3(
            Mathf.Sin(t * 1.0f + _phaseX) * swayStrength * scale,
            Mathf.Sin(t * 0.4f) * swayStrength * 0.3f * scale,
            Mathf.Sin(t * 0.7f + _phaseZ) * swayStrength * scale
        );

        _cloth.randomAcceleration = Vector3.one * randomTurbulence * scale;
    }

    void UpdateHover()
    {
        float noiseY = Mathf.PerlinNoise(_hoverSeedY + Time.time * hoverSpeed, 0.5f);
        _hoverOffset = new Vector3(0f, (noiseY * 2f - 1f) * hoverAmplitude, 0f);

        float noiseRot = Mathf.PerlinNoise(_hoverSeedRot + Time.time * hoverRotationSpeed, 0.5f);
        float tilt = (noiseRot * 2f - 1f) * hoverRotationStrength;
        _hoverRotation = Quaternion.Euler(tilt * 0.4f, 0f, tilt);
    }

    void ApplyVisualTransform()
    {
        if (_visualRoot == null) return;
        _visualRoot.localPosition = _hoverOffset;
        _visualRoot.localRotation = _hoverRotation * _movementTilt;
    }

    void EnforceBounds()
    {
        if (_smr == null) return;
        _smr.updateWhenOffscreen = true;
        _smr.localBounds = new Bounds(
            new Vector3(0f, (-robeHeight + hoodExtension) * 0.5f, 0f),
            new Vector3(hemRadius * 2f + maxClothDistance * 2f,
                        robeHeight + hoodExtension + maxClothDistance,
                        hemRadius * 2f + maxClothDistance * 2f)
        );
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
        head.transform.SetParent(_visualRoot);
        head.transform.localPosition = Vector3.zero;
        head.transform.localScale = Vector3.one * (topRadius * 2f);
        Destroy(head.GetComponent<SphereCollider>());
        if (ghostMaterial != null)
            head.GetComponent<MeshRenderer>().sharedMaterial = ghostMaterial;
    }

    void BuildRobe()
    {
        var robeGO = new GameObject("GhostRobe");
        robeGO.transform.SetParent(_visualRoot);
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

        _smr = robeGO.AddComponent<SkinnedMeshRenderer>();
        _smr.bones = new[] { bone };
        _smr.rootBone = bone;
        _smr.sharedMesh = mesh;

        if (ghostMaterial != null)
        {
            var robeMat = new Material(ghostMaterial);
            robeMat.SetFloat("_Cull", 0f);
            _smr.sharedMaterial = robeMat;
            _baseColor = robeMat.HasProperty("_BaseColor")
            ? robeMat.GetColor("_BaseColor")
            : robeMat.color;
            SetVisibility(0f);
        }

        _cloth = robeGO.AddComponent<Cloth>();
        _cloth.stretchingStiffness = stretchingStiffness;
        _cloth.bendingStiffness = bendingStiffness;
        _cloth.damping = damping;
        _cloth.useGravity = false;
        _cloth.clothSolverFrequency = Mathf.Max(5f, 120f * clothTimeScale);

        ApplyConstraints(_cloth, mesh.vertices);
        SetupBodyColliders();

        _smr.updateWhenOffscreen = true;
        _smr.localBounds = new Bounds(
            new Vector3(0f, (-robeHeight + hoodExtension) * 0.5f, 0f),
            new Vector3(hemRadius * 2f + maxClothDistance * 2f,
                        robeHeight + hoodExtension + maxClothDistance,
                        hemRadius * 2f + maxClothDistance * 2f)
        );
    }

    void SetupBodyColliders()
    {
        var spheres = new SphereCollider[coneSegments + 1];

        for (int i = 0; i <= coneSegments; i++)
        {
            float t = (float)i / coneSegments;
            float y = Mathf.Lerp(coneTopY, coneBottomY, t);
            float r = Mathf.Lerp(coneTopRadius, coneBottomRadius, t);

            var go = new GameObject($"ConeCollider_{i}");
            go.transform.SetParent(_visualRoot);
            go.transform.localPosition = new Vector3(0f, y, 0f);

            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = r;
            sphere.isTrigger = true;

            spheres[i] = sphere;
        }

        var pairs = new ClothSphereColliderPair[coneSegments];
        for (int i = 0; i < coneSegments; i++)
            pairs[i] = new ClothSphereColliderPair(spheres[i], spheres[i + 1]);

        _cloth.sphereColliders = pairs;
    }

    Mesh BuildMesh()
    {
        int vRows = rows + 1;
        var verts = new Vector3[columns * vRows];
        var uvs = new Vector2[columns * vRows];

        float tShoulder = robeHeight / (robeHeight + hoodExtension);

        for (int r = 0; r < vRows; r++)
        {
            float t = (float)r / rows;
            float y, rad;

            if (t <= tShoulder)
            {
                float localT = t / tShoulder;
                y = Mathf.Lerp(-robeHeight, 0f, localT);
                rad = Mathf.Lerp(hemRadius, shoulderRadius, localT);
            }
            else
            {
                float localT = (t - tShoulder) / (1f - tShoulder);
                float domeAngle = localT * Mathf.PI * 0.5f;
                y = hoodExtension * Mathf.Sin(domeAngle);
                rad = shoulderRadius * Mathf.Cos(domeAngle);
            }

            float ridgeFade = t <= tShoulder
                ? 1f
                : 1f - (t - tShoulder) / (1f - tShoulder);

            for (int c = 0; c < columns; c++)
            {
                float angle = (float)c / columns * Mathf.PI * 2f;
                float ridge = Mathf.Sin(angle * ridgeCount) * ridgeDepth * rad * ridgeFade;
                float finalRad = Mathf.Max(0.01f, rad + ridge);

                verts[r * columns + c] = new Vector3(
                    Mathf.Cos(angle) * finalRad,
                    y,
                    Mathf.Sin(angle) * finalRad
                );
                uvs[r * columns + c] = new Vector2((float)c / columns, t);
            }
        }

        var tris = new int[columns * rows * 6];
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int v0 = r * columns + c;
                int v1 = r * columns + (c + 1) % columns;
                int v2 = (r + 1) * columns + c;
                int v3 = (r + 1) * columns + (c + 1) % columns;
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
        float totalHeight = robeHeight + hoodExtension;
        var coeff = new ClothSkinningCoefficient[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            float t = (verts[i].y + robeHeight) / totalHeight;
            if (t >= 1f - pinnedFraction)
            {
                coeff[i].maxDistance = 0f;
                coeff[i].collisionSphereDistance = 0f;
            }
            else
            {
                coeff[i].maxDistance = (1f - t) * maxClothDistance;
                coeff[i].collisionSphereDistance = float.MaxValue;
            }
        }
        cloth.coefficients = coeff;
    }
}