using UnityEngine;

public class DistanceCullingObject : MonoBehaviour
{
    [Header("Culling")]
    [SerializeField] private float visibleDistance = 40f;

    private Transform player;
    private MeshRenderer[] renderers;

    private float sqrVisibleDistance;
    private bool isVisible = true;

    private void Start()
    {
        PlayerFinder.TryAssignIfNull(ref player);

        // BELANGRIJK: MeshRenderer i.p.v. Renderer (fix voor foliage/LOD)
        renderers = GetComponentsInChildren<MeshRenderer>(true);

        sqrVisibleDistance = visibleDistance * visibleDistance;

        Debug.Log($"[CULLING] {gameObject.name} found renderers: {renderers.Length}");

        // force reset naar zichtbaar
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }

        isVisible = true;
    }

    private void Update()
    {
        if (player == null)
        {
            PlayerFinder.TryAssignIfNull(ref player);
            return;
        }

        float sqrDist =
            (player.position - transform.position).sqrMagnitude;

        bool shouldBeVisible = sqrDist <= sqrVisibleDistance;

        if (shouldBeVisible == isVisible)
            return;

        isVisible = shouldBeVisible;

        SetVisible(shouldBeVisible);
    }

    private void SetVisible(bool visible)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }

        Debug.Log($"[CULLING] {gameObject.name} -> {(visible ? "VISIBLE" : "HIDDEN")}");
    }
}