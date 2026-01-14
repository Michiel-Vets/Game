using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 0.10f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float pitch;

    public void ApplyPitch(float mouseY)
    {
        pitch -= mouseY * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
