using UnityEngine;
using UnityEngine.InputSystem; // << nova Input System

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class Player : MonoBehaviour
{
    [Header("Movimento")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;

    [Header("Detecção")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new(0.8f, 0.12f);
    public Transform wallCheck;
    public Vector2 wallCheckSize = new(0.12f, 0.8f);
    public LayerMask groundMask;

    // Estados p/ animação
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public bool isOnWall;
    [HideInInspector] public bool isMoving;
    [HideInInspector] public float xSpeed;

    Rigidbody2D rb;
    SpriteRenderer sr;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.None;
    }

    void Update()
    {
        float inputX = ReadHorizontal();
        isMoving = Mathf.Abs(inputX) > 0.01f;

        if (WasJumpPressed() && isGrounded)
        {
            var v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;
        }

        if (isMoving)
            sr.flipX = inputX < 0f;

        // aplica velocidade horizontal simples no Update? melhor no FixedUpdate:
        _cachedInputX = inputX;
    }

    float _cachedInputX;

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(_cachedInputX * moveSpeed, rb.linearVelocity.y);
        xSpeed = rb.linearVelocity.x;

        if (groundCheck)
        {
            isGrounded = true;
        } else
        {
            isGrounded = false;
        }

       

        if (wallCheck)        
        {
            isOnWall = true;
        } else
        {
            isOnWall = false;   
        }
    }

    // --- Leitura simples com Input System ---
    float ReadHorizontal()
    {
        float x = 0f;

        // Teclado (A/D, seta esq/dir)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        }

        // Gamepad (stick e dpad)
        var gp = Gamepad.current;
        if (gp != null)
        {
            float stick = gp.leftStick.ReadValue().x;
            if (Mathf.Abs(stick) > Mathf.Abs(x)) x = stick; // prioriza stick
            if (gp.dpad.left.isPressed)  x = Mathf.Min(x, -1f);
            if (gp.dpad.right.isPressed) x = Mathf.Max(x,  1f);
        }

        return Mathf.Clamp(x, -1f, 1f);
    }

    bool WasJumpPressed()
    {
        // Espaço / W / seta ↑ / Botão sul do gamepad (A / Cross)
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
            return true;

        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame)
            return true;

        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (groundCheck) Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        Gizmos.color = Color.red;
        if (wallCheck) Gizmos.DrawWireCube(wallCheck.position, wallCheckSize);
    }
}
