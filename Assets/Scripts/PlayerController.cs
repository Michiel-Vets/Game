using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthController healthController;
    [SerializeField] private StaminaController staminaController;
    [SerializeField] private CameraLook cameraLook;
    [SerializeField] private FlashlightController flashlightController;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 9f;
    [SerializeField] private float lookSensitivity = 0.1f;

    [Header("Jumping")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float minJumpVelocity = -0.1f;
    [SerializeField] private float maxJumpVelocity = 0.5f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDecay = 8f;

    [Header("Debug Health Input")]
    [SerializeField] private float healthChangePerSecond = 25f;

    private Rigidbody rb;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 knockbackVelocity;

    private bool controlsEnabled = true;
    private bool isSprinting;
    private bool jumpRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void OnEnable()
    {
        if (healthController != null)
            healthController.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (healthController != null)
            healthController.OnDeath -= HandleDeath;
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        if (!controlsEnabled)
            return;

        HandleSprintKeyboardFallback();
        HandleJumpKeyboardFallback();
        HandleFlashlightKeyboardFallback();
        HandleDebugHealthInput();
        UpdateStamina();
    }

    private void FixedUpdate()
    {
        if (!controlsEnabled)
            return;

        HandleRotation();
        HandleMovement();
        HandleJump();
        DecayKnockback();
    }

    public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();

        if (controlsEnabled && cameraLook != null)
            cameraLook.ApplyLook(lookInput.y * lookSensitivity);
    }

    public void OnSprint(InputValue value) => isSprinting = value.isPressed;

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            jumpRequested = true;
    }

    public void ApplyKnockback(Vector3 direction, float force, float upwardForce)
    {
        if (direction.sqrMagnitude < 0.01f)
            return;

        direction.y = 0f;
        direction.Normalize();

        knockbackVelocity += direction * force;
        rb.AddForce(Vector3.up * upwardForce, ForceMode.Impulse);
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;

        if (!enabled)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            knockbackVelocity = Vector3.zero;
            isSprinting = false;
            jumpRequested = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void HandleMovement()
    {
        Vector3 worldDirection = transform.TransformDirection(new Vector3(moveInput.x, 0f, moveInput.y));

        float speed;
        bool canSprint = isSprinting && (staminaController == null || !staminaController.IsExhausted);
        speed = canSprint ? sprintSpeed : moveSpeed;

        Vector3 horizontal = worldDirection.normalized * speed + knockbackVelocity;
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
    }

    private void HandleJump()
    {
        if (!jumpRequested)
            return;

        jumpRequested = false;

        // Geen springen als stamina leeg is
        if (staminaController != null && staminaController.IsExhausted)
            return;

        float yVelocity = rb.linearVelocity.y;

        if (yVelocity < minJumpVelocity || yVelocity > maxJumpVelocity)
            return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        staminaController?.DrainStaminaForJump();
    }

    private void HandleRotation()
    {
        float yaw = lookInput.x * lookSensitivity;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw, 0f));
    }

    private void DecayKnockback()
    {
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.fixedDeltaTime);
    }

    private void UpdateStamina()
    {
        if (staminaController == null)
            return;

        staminaController.SetShiftHeld(isSprinting);

        bool isActuallySprinting = isSprinting && !staminaController.IsExhausted
                                   && moveInput.sqrMagnitude > 0.01f;

        if (isActuallySprinting)
            staminaController.DrainStamina(Time.deltaTime);
        else
            staminaController.RechargeStamina(Time.deltaTime);
    }

    /// <summary>
    /// Stamina als fractie (0–1), doorgestuurd vanuit StaminaController.
    /// </summary>
    public float StaminaFraction => staminaController != null ? staminaController.StaminaFraction : 0f;

    private void HandleSprintKeyboardFallback()
    {
        if (Keyboard.current == null)
            return;

        isSprinting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
    }

    private void HandleJumpKeyboardFallback()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpRequested = true;
    }

    private void HandleFlashlightKeyboardFallback()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && flashlightController != null)
            flashlightController.Toggle();
    }

    private void HandleDebugHealthInput()
    {
        if (healthController == null || !healthController.IsAlive || Keyboard.current == null)
            return;

        float delta = healthChangePerSecond * Time.deltaTime;

        if (Keyboard.current.qKey.isPressed)
            healthController.TakeDamage(delta);

        if (Keyboard.current.eKey.isPressed)
            healthController.Heal(delta);
    }

    /// <summary>
    /// Called when the player dies. GameOverScreen owns disabling controls and stopping time.
    /// This method only handles what is strictly PlayerController's own responsibility.
    /// </summary>
    private void HandleDeath()
    {
        // Input is killed by GameOverScreen. Nothing extra needed here.
        // Hook kept so future per-controller death VFX/animation can be added.
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}