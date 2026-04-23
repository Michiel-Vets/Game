using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float currentPitch;

    public void ApplyLook(float mouseY)
    {
        // Ik kijk omhoog of omlaag met de muis.
        currentPitch -= mouseY * mouseSensitivity;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }

    public void ResetLook()
    {
        // Ik zet mijn verticale camera terug neutraal.
        currentPitch = 0f;
        transform.localRotation = Quaternion.identity;
    }
}