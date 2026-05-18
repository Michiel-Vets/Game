using UnityEngine;

public class PickupVisibility : MonoBehaviour
{
    [SerializeField] private float visibilityDistance = 20f;

    private Renderer[] _renderers;
    private Transform _player;

    private void Start()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _player = PlayerFinder.FindPlayer();
    }

    private void Update()
    {
        if (_player == null)
            _player = PlayerFinder.FindPlayer();
        if (_player == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);
        float alpha = dist >= visibilityDistance ? 0f : Mathf.Clamp01(1f - (dist / visibilityDistance));

        foreach (Renderer r in _renderers)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            r.SetPropertyBlock(block);
        }
    }
}