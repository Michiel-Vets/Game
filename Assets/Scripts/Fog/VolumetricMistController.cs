using UnityEngine;
using System.Collections.Generic;

public class VolumetricMistController : MonoBehaviour
{
    public static VolumetricMistController Instance { get; private set; }

    private static readonly List<Transform> _displacers = new List<Transform>();
    private readonly Vector4[] _positionBuffer = new Vector4[16];

    [Header("Fog Density During Wave/Break")]
    [Tooltip("Naam van de shader global float voor fog dichtheid (pas aan naar jouw shader).")]
    [SerializeField] private string fogDensityParam = "_FogDensity";
    [SerializeField] private float waveFogDensity  = 1.0f;
    [SerializeField] private float breakFogDensity = 0.35f;
    [SerializeField] private float fogTransitionSpeed = 0.5f;

    private float _targetDensity;
    private float _currentDensity;

    public static void Register(Transform t)
    {
        if (!_displacers.Contains(t)) _displacers.Add(t);
    }

    public static void Unregister(Transform t) => _displacers.Remove(t);

    private void Awake()
    {
        Instance = this;
        _currentDensity = waveFogDensity;
        _targetDensity  = waveFogDensity;
    }

    /// <summary>Roep aan vanuit WaveManager bij begin/einde van een wave.</summary>
    public void SetBreakMode(bool isBreak)
    {
        _targetDensity = isBreak ? breakFogDensity : waveFogDensity;
    }

    private void Update()
    {
        // Displacer posities doorgeven aan shader
        int count = 0;
        for (int i = 0; i < _displacers.Count; i++)
        {
            if (_displacers[i] == null) { _displacers.RemoveAt(i--); continue; }
            if (count >= 16) break;
            _positionBuffer[count++] = _displacers[i].position;
        }

        Shader.SetGlobalVectorArray("_DisplacerPositions", _positionBuffer);
        Shader.SetGlobalFloat("_DisplacerCount", (float)count);

        // Fog dichtheid vloeiend overgaan
        _currentDensity = Mathf.MoveTowards(
            _currentDensity, _targetDensity, fogTransitionSpeed * Time.deltaTime);
        Shader.SetGlobalFloat(fogDensityParam, _currentDensity);
    }
}
