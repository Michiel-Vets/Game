using UnityEngine;

public static class VisibilityChecker
{
    public static bool IsTransformVisibleToCamera(Transform target, Transform camera, float dotThreshold)
    {
        if (target == null || camera == null)
            return false;

        Vector3 toTarget = (target.position - camera.position).normalized;
        float dot = Vector3.Dot(camera.forward, toTarget);
        return dot >= dotThreshold;
    }
}