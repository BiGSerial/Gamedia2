using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(GroundSensor2D))]
public class PlayerJump : MonoBehaviour
{
    public enum DoubleMode { SetVelocity, AddImpulseAbsolute, AddImpulseScaled }

    [Header("Jump")]
    public float jumpForce = 6f;

    [Header("Double Jump")]
    public DoubleMode doubleMode = DoubleMode.SetVelocity;

    [Tooltip("Usado quando doubleMode = SetVelocity")]
    public float doubleJumpVelocityY = 10f;

    [Tooltip("Usado quando doubleMode = AddImpulseAbsolute")]
    public float doubleJumpImpulse = 6f;

    [Tooltip("Usado quando doubleMode = AddImpulseScaled (impulso = jumpForce * dblJumpMultiplier)")]
    public float dblJumpMultiplier = 1.2f;

    [Header("Feel Improvements")]
    [Tooltip("Tolerância após sair do chão ainda permitindo pular")]
    public float coyoteTime = 0.12f;
    [Tooltip("Tolerância para aproveitar um clique antecipado de pulo")]
    public float jumpBufferTime = 0.12f;
    [Tooltip("Tempo ignorando ground após impulsionar (evita apagar 'Jump' cedo)")]
    public float leaveGroundSuppress = 0.12f;
    [Tooltip("Delay mínimo entre pulo do chão e aceitar duplo (evita double-tap instantâneo)")]
    public float minDelayForDouble = 0.05f;

    [Header("Audio (opcional)")]
    public AudioSource jumpSound;

    Rigidbody2D rb;
    Animator animator;
    GroundSensor2D sensor;

    bool canDoubleJump;
    float lastJumpPressedTime = -999f;
    float lastGroundJumpTime  = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sensor = GetComponent<GroundSensor2D>();
    }

    void Update()
    {
        TryConsumeBufferedJump();
    }

    void FixedUpdate()
    {
        if (animator) animator.SetBool("Ground", sensor.IsGrounded);

        if (sensor.JustLanded)
        {
            canDoubleJump = false;
            if (animator)
            {
                animator.SetBool("Jump", false);
                animator.SetBool("dblJump", false);
                animator.SetBool("Fall", false);
            }
        }
        else if (!sensor.IsGrounded)
        {
            bool falling = rb.linearVelocity.y < -2f;
            if (animator)
            {
                animator.SetBool("Fall", falling);
                if (falling)
                {
                    animator.SetBool("Jump", false);
                    animator.SetBool("dblJump", false);
                }
            }
        }
        else if (animator)
        {
            animator.SetBool("Fall", false);
        }
    }

    /// Chame do Controller quando apertar pulo.
    public void BufferJump()
    {
        lastJumpPressedTime = Time.time;
    }

    void TryConsumeBufferedJump()
    {
        if (Time.time - lastJumpPressedTime > jumpBufferTime) return;

        bool groundedOrCoyote =
            sensor.IsGrounded ||
            (Time.time - sensor.LastTimeGrounded) <= coyoteTime;

        if (groundedOrCoyote)
        {
            PerformGroundJump();
            lastJumpPressedTime = -999f;
            return;
        }

        if (!sensor.IsGrounded && canDoubleJump && (Time.time - lastGroundJumpTime) >= minDelayForDouble)
        {
            PerformDoubleJump();
            lastJumpPressedTime = -999f;
        }
    }

    void PerformGroundJump()
    {
        // zera vel.y pra resposta consistente
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        sensor.SuppressGround(leaveGroundSuppress);
        canDoubleJump = true;
        lastGroundJumpTime = Time.time;

        if (animator) animator.SetBool("Jump", true);
        if (jumpSound) jumpSound.Play();
    }

    void PerformDoubleJump()
    {
        // Garante “novo impulso”: escolha do modo
        float currentVelY = rb.linearVelocity.y;
        switch (doubleMode)
        {
            case DoubleMode.SetVelocity:
                float targetY = Mathf.Max(currentVelY, doubleJumpVelocityY);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, targetY);
                break;

            case DoubleMode.AddImpulseAbsolute:
                float baseY = Mathf.Max(currentVelY, 0f); // só zera se estiver caindo
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, baseY);
                rb.AddForce(Vector2.up * doubleJumpImpulse, ForceMode2D.Impulse);
                break;

            case DoubleMode.AddImpulseScaled:
                float scaledBaseY = Mathf.Max(currentVelY, 0f);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, scaledBaseY);
                rb.AddForce(Vector2.up * (jumpForce * Mathf.Max(0.01f, dblJumpMultiplier)), ForceMode2D.Impulse);
                break;
        }

        canDoubleJump = false;
        if (sensor != null)
            sensor.SuppressGround(leaveGroundSuppress);

        if (animator)
        {
            animator.SetBool("dblJump", true);
            animator.SetBool("Jump", false);
        }
        if (jumpSound) jumpSound.Play();
    }
}
