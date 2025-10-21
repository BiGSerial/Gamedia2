using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(GroundSensor2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Player Movement")]
    public float speed = 5f;

    [Header("Sprint")]
    public float sprintMultiplier = 2f;
    [Range(1f, 2.5f)] public float runAnimSpeed = 1.4f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Animator animator;
    GroundSensor2D sensor;

    bool sprintLatched;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        sensor = GetComponent<GroundSensor2D>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.None; // pixel-art
    }

    public void Tick(float axisX, bool sprintHeld)
    {
        bool isMoving = Mathf.Abs(axisX) > 0.0001f;

        // Sprint latch: liga/desliga no chão
        if (sensor && sensor.IsGrounded) sprintLatched = isMoving && sprintHeld;
        else                              sprintLatched = sprintLatched && sprintHeld;

        float currentSpeed = speed * (sprintLatched ? sprintMultiplier : 1f);

        // Movimento X
        rb.linearVelocity = new Vector2(axisX * currentSpeed, rb.linearVelocity.y);

        // Animator: andar + velocidade de anim
        if (animator)
        {
            animator.SetBool("MoveX", isMoving);
            bool grounded = sensor ? sensor.IsGrounded : true;
            animator.speed = (isMoving && grounded) ? (sprintLatched ? runAnimSpeed : 1f) : 1f;
        }

        // Flip visual
        if (isMoving) sr.flipX = axisX < 0f;
    }
}
