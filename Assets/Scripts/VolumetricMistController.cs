using UnityEngine;
using System.Collections.Generic;

public class VolumetricMistController : MonoBehaviour
{
    private static readonly List<Transform> _displacers = new List<Transform>();
    private readonly Vector4[] _positionBuffer = new Vector4[16];

    public static void Register(Transform t)
    {
        if (!_displacers.Contains(t)) _displacers.Add(t);
    }

    public static void Unregister(Transform t) => _displacers.Remove(t);

    private void Update()
    {
        int count = 0;
        for (int i = 0; i < _displacers.Count; i++)
        {
            if (_displacers[i] == null) { _displacers.RemoveAt(i--); continue; }
            if (count >= 16) break;
            _positionBuffer[count++] = _displacers[i].position;
        }

        Shader.SetGlobalVectorArray("_DisplacerPositions", _positionBuffer);
        Shader.SetGlobalFloat("_DisplacerCount", (float)count);

        if (Time.frameCount % 60 == 0)
            Debug.Log($"[MistController] Actieve displacers: {count}");
    }
}