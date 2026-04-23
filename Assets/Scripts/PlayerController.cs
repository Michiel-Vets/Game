using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthController healthController;
    [SerializeField] private CameraLook cameraLook;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 0.1f;

    [Header("Debug Health Input")]
    [SerializeField] private float healthChangePerSecond = 25f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool controlsEnabled = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    private void OnEnable()
    {
        if (healthController != null)
        {
            healthController.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (healthController != null)
        {
            healthController.OnDeath -= HandleDeath;
        }
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        if (!controlsEnabled)
            return;

        HandleDebugHealthInput();
    }

    private void FixedUpdate()
    {
        if (!controlsEnabled)
            return;

        HandleRotation();
        HandleMovement();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();

        if (!controlsEnabled || cameraLook == null)
            return;

        // Ik kijk links, rechts, omhoog en omlaag.
        cameraLook.ApplyLook(lookInput.y);
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;

        if (!enabled)
        {
            // Ik stop meteen met bewegen wanneer mijn controls uit gaan.
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void HandleMovement()
    {
        Vector3 localDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 worldDirection = transform.TransformDirection(localDirection);

        Vector3 targetPosition = rb.position + worldDirection * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }

    private void HandleRotation()
    {
        float yaw = lookInput.x * lookSensitivity;
        Quaternion deltaRotation = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    private void HandleDebugHealthInput()
    {
        if (healthController == null || !healthController.IsAlive)
            return;

        // Ik gebruik A om schade te nemen en E om te healen tijdens het testen.
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed)
            {
                healthController.TakeDamage(healthChangePerSecond * Time.deltaTime);
            }

            if (Keyboard.current.eKey.isPressed)
            {
                healthController.Heal(healthChangePerSecond * Time.deltaTime);
            }
        }
    }

    private void HandleDeath()
    {
        // Ik kan niets meer doen wanneer ik dood ben.
        SetControlsEnabled(false);
        UnlockCursor();
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}