using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class Player : MonoBehaviour
{
    [Header("Player Movement")]
    public float speed = 5f;
    public float jumpForce = 1f;
    public float dblJumpMultiplier = 2f;

    [Header("Sprint")]
    public float sprintMultiplier = 2f;
    [Range(1f, 2.5f)] public float runAnimSpeed = 1.4f;

    [Header("Ground Check")]
    [SerializeField] Collider2D feetTrigger; // arraste o collider do pé aqui
    [SerializeField] LayerMask groundMask;

    public AudioSource jumpSound;
    public AudioSource footstepSound;

    Rigidbody2D rb;
    Animator animator;
    SpriteRenderer sr;

    bool isGrounded = false;
    bool wasGrounded = false;   // <— usado para detectar a transição ar→chão
    bool canDoubleJump = false;
    bool sprintLatched = false;

    float horizontalInput;
    bool sprintKey;
    bool jumpPressed;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.None; // pixel-art
    }

    void Update()
    {
        ReadInput();
        Move();
        Jump();
        UpdateFallFlag();  // garante Fall/Jump consistentes no ar
    }

    void ReadInput()
    {
        horizontalInput = 0f;
        sprintKey = false;
        jumpPressed = false;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  horizontalInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontalInput += 1f;
            sprintKey = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                jumpPressed = true;
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            float stickX = gp.leftStick.ReadValue().x;
            if (Mathf.Abs(stickX) > Mathf.Abs(horizontalInput)) horizontalInput = stickX;
            sprintKey = sprintKey || gp.leftShoulder.isPressed || gp.rightTrigger.ReadValue() > 0.5f;
            if (gp.buttonSouth.wasPressedThisFrame) jumpPressed = true;
        }

        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
    }

    void Move()
    {
        bool isMoving = Mathf.Abs(horizontalInput) > 0.0001f;

        // sprint "latch": só liga/desliga no chão
        if (isGrounded) sprintLatched = isMoving && sprintKey;
        else            sprintLatched = sprintLatched && sprintKey;

        float currentSpeed = speed * (sprintLatched ? sprintMultiplier : 1f);

        // VELOCIDADE CORRETA
        rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);

        // Animator
        if (animator)
        {
            animator.SetBool("MoveX", isMoving); // bool para andar/parar
            animator.speed = (isMoving && isGrounded) ? (sprintLatched ? runAnimSpeed : 1f) : 1f;
        }

        // flip visual
        if (isMoving) sr.flipX = horizontalInput < 0f;
    }

    void Jump()
    {
        if (!jumpPressed) return;

        if (isGrounded)
        {
            rb.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);
            isGrounded = false;
            canDoubleJump = true;

            if (animator) animator.SetBool("Jump", true);
            if (jumpSound) jumpSound.Play();
        }
        else if (canDoubleJump)
        {
            rb.AddForce(new Vector2(0f, jumpForce / dblJumpMultiplier), ForceMode2D.Impulse);
            canDoubleJump = false;

            if (animator)
            {
                animator.SetBool("dblJump", true);
                animator.SetBool("Jump", false);
            }
            if (jumpSound) jumpSound.Play();
        }
    }

    void FixedUpdate()
    {
        // Verificação de chão usando o collider dos pés
        bool groundedNow = feetTrigger && feetTrigger.IsTouchingLayers(groundMask);
        isGrounded = groundedNow;

        // Atualiza flag Ground
        if (animator) animator.SetBool("Ground", isGrounded);

        // ======= Correção: ao detectar chão, limpa Jump/dblJump/Fall imediatamente =======
        if (isGrounded)
        {
            // Resetar estados de salto/queda assim que tocar o chão
            if (animator)
            {
                animator.SetBool("Jump", false);
                animator.SetBool("dblJump", false);
                animator.SetBool("Fall", false);
            }

            // Após pousar, não deve manter o "direito" de duplo pulo
            canDoubleJump = false;

            // Evento de pouso (opcional)
            if (!wasGrounded)
            {
                // exemplo: tocar um som de pouso, emitir poeira etc.
                // if (footstepSound && Mathf.Abs(rb.linearVelocity.x) > 0.1f) footstepSound.Play();
            }
        }

        // guarda o estado para detectar a transição no próximo frame de física
        wasGrounded = isGrounded;
    }

    void UpdateFallFlag()
    {
        // queda sensível: basta estar no ar e vel.y < 0
        bool falling = !isGrounded && rb.linearVelocity.y < -2f;

        if (animator)
        {
            animator.SetBool("Fall", falling);
            if (falling)
            {
                // ao começar a cair, garanta que Jump não fique travado
                animator.SetBool("Jump", false);
                animator.SetBool("dblJump", false);
            }
        }
    }
}
