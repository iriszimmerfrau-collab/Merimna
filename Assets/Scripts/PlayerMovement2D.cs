using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top-down 2D WASD movement using a Rigidbody2D.
/// W/A/S/D to move in 8 directions, Left Shift to sprint.
/// Works with both the new Input System and the legacy Input Manager.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float sprintSpeed = 8f;

    [Header("Sprite (optional)")]
    [Tooltip("Flips the sprite to face left/right. Leave empty to auto-find one on this object.")]
    [SerializeField] SpriteRenderer spriteRenderer;

    Rigidbody2D rb;
    Vector2 moveInput;
    bool sprinting;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Top-down setup: no falling, no spinning when bumping into walls.
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // smoother motion

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // Read input every frame so no key presses get missed.
        moveInput = ReadMoveInput();
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize(); // no faster diagonal movement

        sprinting = ReadSprint();

        // Face left or right based on horizontal movement.
        if (spriteRenderer != null && moveInput.x != 0f)
            spriteRenderer.flipX = moveInput.x < 0f;
    }

    void FixedUpdate()
    {
        // Apply movement in the physics step so collisions behave.
        float speed = sprinting ? sprintSpeed : walkSpeed;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = moveInput * speed;
#else
        rb.velocity = moveInput * speed;
#endif
    }

    // ---------- Input ----------

    Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
#else
        float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
#endif
        return new Vector2(x, y);
    }

    bool ReadSprint()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift);
#endif
    }
}