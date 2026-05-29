using UnityEngine;

/// <summary>
/// Zet shader-globals die een rechthoekig pad door de mist vrijmaken.
/// Geen collider/hitbox. Werkt samen met de corridor-sectie in VolumetricMist.shader.
/// </summary>
public class FogCorridor : MonoBehaviour
{
    private static readonly int _propActive      = Shader.PropertyToID("_CorridorActive");
    private static readonly int _propStartEndXZ  = Shader.PropertyToID("_CorridorStartEndXZ");
    private static readonly int _propHalfWidth   = Shader.PropertyToID("_CorridorHalfWidth");
    private static readonly int _propHeight      = Shader.PropertyToID("_CorridorHeight");

    [SerializeField] private float halfWidth = 2f;   // 4 m breed totaal
    [SerializeField] private float height    = 10f;  // 10 m hoog

    private bool _active;

    /// <summary>Activeer het pad van <paramref name="from"/> naar <paramref name="to"/>.</summary>
    public void Activate(Vector3 from, Vector3 to)
    {
        _active = true;
        Shader.SetGlobalFloat(_propActive, 1f);
        Shader.SetGlobalVector(_propStartEndXZ, new Vector4(from.x, from.z, to.x, to.z));
        Shader.SetGlobalFloat(_propHalfWidth, halfWidth);
        Shader.SetGlobalFloat(_propHeight, height);
    }

    private void OnDestroy()
    {
        if (_active)
            Shader.SetGlobalFloat(_propActive, 0f);
    }
}
