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
        currentPitch = Mathf.Clamp(currentPitch - mouseY * mouseSensitivity, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }

    public void ResetLook()
    {
        currentPitch = 0f;
        transform.localRotation = Quaternion.identity;
    }
}