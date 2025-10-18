// Assets/_Project/Scripts/Enemies/EnemyWalker.cs
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyWalker : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] float speed = 2.0f;
    [Tooltip("1 = direita, -1 = esquerda")]
    [SerializeField] int direction = -1;

    [Header("Detecção de borda/parede")]
    [SerializeField] Transform groundCheck;        // empty na ponta do pé, um pouco à frente
    [SerializeField] Transform wallCheck;          // empty à frente, altura do peito
    [SerializeField] float groundCheckDistance = 0.25f;
    [SerializeField] float wallCheckDistance = 0.12f;
    [SerializeField] LayerMask groundLayer;        // layer do chão/plataforma
    [SerializeField] LayerMask wallMask;           // layer da parede (pode ser o mesmo do chão)

    [Header("Stomp (pisão do player)")]
    [SerializeField] float stompYOffset = 0.1f;    // quanto acima do inimigo o player precisa estar
    [SerializeField] float stompBounce = 8f;       // quique no player após matar

    [Header("Morte com pulo + queda")]
    [SerializeField] bool destroyOnDeath = true;   // destruir objeto após cair
    [SerializeField] float deathJumpY = 5.5f;      // impulso vertical ao morrer
    [SerializeField] float deathKnockbackX = 1.5f; // empurrão horizontal ao morrer (±)
    [SerializeField] float deathGravity = 4.5f;    // gravidade enquanto cai morto
    [SerializeField] float deathTorque = 25f;      // rotação (0 = sem girar)
    [SerializeField] float offscreenMargin = 1.0f; // margem abaixo da câmera para destruir
    [SerializeField] float deathTimeout = 4.0f;    // segurança para destruir

    [Header("Dano no Player (lateral/baixo)")]
    [SerializeField] float hitCooldown = 0.35f;    // janela antiduplo-hit
    [SerializeField] float hitKnockbackX = 6f;     // empurrão no player ao tomar dano
    [SerializeField] string playerTag = "Player";  // tag do player

    [Header("Animação (opcional)")]
    [SerializeField] Animator animator;            // arraste o Animator aqui, se houver
    [SerializeField] string walkBool = "walk";
    [SerializeField] string deadTrigger = "dead";

    [Header("Áudio (opcional)")]
    public AudioSource deathSound;

    [Header("Eventos (opcionais, ligue no Inspector)")]
    public UnityEvent onDie;                       // chamado ao morrer
    public UnityEvent onPlayerHit;                 // chamado ao dar dano no player
    public UnityEvent<Vector2> onPlayerBounce;     // passa posição do inimigo

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    Collider2D[] allColliders;

    bool isDead = false;
    float lastHitTime = -999f;

    // offsets locais originais dos sensores (pra espelhar no Flip)
    Vector3 groundChkLocal0, wallChkLocal0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        allColliders = GetComponentsInChildren<Collider2D>();

        direction = direction < 0 ? -1 : 1;

        if (groundCheck) groundChkLocal0 = groundCheck.localPosition;
        if (wallCheck)   wallChkLocal0   = wallCheck.localPosition;
        UpdateSensorOffsets();

        // Recomendações comuns no Rigidbody2D (ajuste no Inspector se quiser):
        // rb.freezeRotation = true;
        // rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        if (isDead) return;
        Patrol();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (isDead) return;
        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
    }

    void LateUpdate()
    {
        if (isDead) return;
        if (sr) sr.flipX = (direction < 0); // garante flip após Animator
    }

    void Patrol()
    {
        // chão à frente
        bool hasGroundAhead = false;
        if (groundCheck)
        {
            RaycastHit2D groundHit = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, groundLayer);
            hasGroundAhead = groundHit.collider != null;
        }

        // parede à frente
        bool hasWallAhead = false;
        if (wallCheck)
        {
            Vector2 lookDir = new Vector2(direction, 0f);
            RaycastHit2D wallHit = Physics2D.Raycast(wallCheck.position, lookDir, wallCheckDistance, wallMask);
            if (wallHit.collider != null)
            {
                // considera parede se a normal se opõe à direção de movimento
                float facing = Mathf.Sign(direction);
                hasWallAhead = wallHit.normal.x * facing < -0.2f;
            }
        }

        if (!hasGroundAhead || hasWallAhead)
            Flip();
    }

    void UpdateAnimator()
    {
        if (!animator) return;
        animator.SetBool(walkBool, Mathf.Abs(rb.linearVelocity.x) > 0.05f);
    }

    void Flip()
    {
        direction *= -1;
        UpdateSensorOffsets();
    }

    void UpdateSensorOffsets()
    {
        if (groundCheck)
            groundCheck.localPosition = new Vector3(Mathf.Abs(groundChkLocal0.x) * direction, groundChkLocal0.y, groundChkLocal0.z);
        if (wallCheck)
            wallCheck.localPosition   = new Vector3(Mathf.Abs(wallChkLocal0.x)   * direction, wallChkLocal0.y,   wallChkLocal0.z);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (!collision.collider.CompareTag(playerTag)) return;

        Rigidbody2D prb = collision.rigidbody;

        // Critérios de "stomp": player acima + descendo (ou velocidade Y <= 0)
        bool playerAbove = collision.transform.position.y > (transform.position.y + stompYOffset);
        bool playerDescending = prb ? (prb.linearVelocity.y <= 0f) : true;

        if (playerAbove && playerDescending)
        {
            // ====== PISÃO: mata o inimigo e quica o player
            Die();

            if (deathSound) deathSound.Play();

            if (prb) prb.linearVelocity = new Vector2(prb.linearVelocity.x, stompBounce);

            onPlayerBounce?.Invoke(transform.position);
        }
        else
        {
            // ====== DANO NO PLAYER (lateral/por baixo)
            if (Time.time - lastHitTime >= hitCooldown)
            {
                lastHitTime = Time.time;

                if (prb)
                {
                    float dir = Mathf.Sign(prb.position.x - rb.position.x); // empurra pra fora do inimigo
                    prb.linearVelocity = new Vector2(dir * hitKnockbackX, prb.linearVelocity.y);
                }

                onPlayerHit?.Invoke();
                // o que acontece com o player (vida, invencibilidade) você conecta via evento
            }
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1) desativa colisão para atravessar o piso
        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
                if (allColliders[i]) allColliders[i].enabled = false;
        }

        // 2) física de morte
        rb.constraints = RigidbodyConstraints2D.None;          // libera rotação/translação
        rb.bodyType = RigidbodyType2D.Dynamic;                  // garante dinâmico
        rb.gravityScale = deathGravity;                         // mais pesado
        rb.linearVelocity = Vector2.zero;

        float dirKnock = Random.value < 0.5f ? -1f : 1f;
        rb.AddForce(new Vector2(dirKnock * deathKnockbackX, deathJumpY), ForceMode2D.Impulse);

        if (Mathf.Abs(deathTorque) > 0.01f)
            rb.AddTorque(deathTorque * -dirKnock, ForceMode2D.Impulse);

        // 3) animação
        if (animator && !string.IsNullOrEmpty(deadTrigger))
            animator.SetTrigger(deadTrigger);

        // 4) eventos
        onDie?.Invoke();

        // 5) destruir fora da tela (ou por timeout)
        if (destroyOnDeath)
            InvokeRepeating(nameof(CheckOffscreenAndDestroy), 0.15f, 0.15f);

        Destroy(gameObject, deathTimeout); // segurança
    }

    void CheckOffscreenAndDestroy()
    {
        var cam = Camera.main;
        if (!cam) { Destroy(gameObject); return; }

        float camBottom = cam.transform.position.y - cam.orthographicSize;
        if (transform.position.y < camBottom - offscreenMargin)
            Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 dir = new Vector3(Mathf.Sign(direction), 0f, 0f);
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + dir * wallCheckDistance);
        }
    }
}
