using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody rb;

    private float movementX;
    private float movementY;
    private float lookX;
    private float lookY;

    [SerializeField] private CameraLook cameraLook;

    public float maxSpeed = 5f;
    public float acceleration = 20f;
    public float deceleration = 15f;
    public float mouseSensitivity = 0.10f;

    private Vector3 currentVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnMove(InputValue movementValue)
    {
        Vector2 movementVector = movementValue.Get<Vector2>();
        movementX = movementVector.x;
        movementY = movementVector.y;
    }

    void OnLook(InputValue lookValue)
    {
        Vector2 look = lookValue.Get<Vector2>();
        lookX = look.x;
        lookY = look.y;

        cameraLook.ApplyPitch(lookY);
    }

    private void FixedUpdate()
    {
        float yaw = lookX * mouseSensitivity;
        Quaternion turnRotation = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);

        Vector3 inputDirection = new Vector3(movementX, 0f, movementY);
        Vector3 worldDirection = transform.TransformDirection(inputDirection);

        Vector3 targetVelocity = worldDirection.normalized * maxSpeed;

        float accel = inputDirection.sqrMagnitude > 0.01f
            ? acceleration
            : deceleration;

        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            accel * Time.fixedDeltaTime
        );

        rb.MovePosition(
            rb.position + currentVelocity * Time.fixedDeltaTime
        );
    }
}
