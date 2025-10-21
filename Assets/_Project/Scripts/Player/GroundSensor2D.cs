using UnityEngine;

public class GroundSensor2D : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private Collider2D feetTrigger; // arraste o collider do pé aqui
    [SerializeField] private LayerMask groundMask = ~0;

    public bool IsGrounded { get; private set; }
    public bool WasGrounded { get; private set; }
    public bool JustLanded { get; private set; }      // <— NOVO: true somente no frame do pouso
    public float LastTimeGrounded { get; private set; } = -999f;

    float suppressGroundUntil = -999f;

    void FixedUpdate()
    {
        WasGrounded = IsGrounded;

        bool touching = feetTrigger && feetTrigger.IsTouchingLayers(groundMask);
        bool suppressed = Time.time < suppressGroundUntil;

        IsGrounded = touching && !suppressed;

        // pouso detectado no frame
        JustLanded = (!WasGrounded && IsGrounded);

        if (IsGrounded)
            LastTimeGrounded = Time.time;
    }

    /// Ignora detecção de ground por 'duration' segundos (chame quando impulsionar o pulo).
    public void SuppressGround(float duration)
    {
        suppressGroundUntil = Mathf.Max(suppressGroundUntil, Time.time + Mathf.Max(0f, duration));
    }

    public void Configure(Collider2D feet, LayerMask mask)
    {
        feetTrigger = feet;
        groundMask = mask;
    }
}
