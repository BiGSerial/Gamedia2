using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ChickeFly : MonoBehaviour
{
    [Header("Death Settings")]
    [SerializeField] private bool isDead = false;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float deathJumpY = 5.5f;
    [SerializeField] private float deathKnockbackX = 1.5f;
    [SerializeField] private float deathGravity = 4.5f;
    [SerializeField] private float deathTorque = 25f;
    [SerializeField] private float offscreenMargin = 1.0f;
    [SerializeField] private float deathTimeout = 4.0f;
    [SerializeField] private Animator animator;           // opcional
    [SerializeField] private string deadTrigger = "dead"; // opcional
    public AudioSource deathSound;                        // opcional

    [Header("Stomp (pulo na cabeça)")]
    [SerializeField] private float stompBounce = 8f;

    [Header("Trigger Settings")]
    [SerializeField] private BoxCollider2D headTrigger;
    [SerializeField] private float triggerHeight = 0.5f;  // posição acima do centro
    [SerializeField] private Vector2 triggerSize = new Vector2(0.8f, 0.3f);

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float changeDirectionTime = 1f;
    [SerializeField] private float verticalDistance = 3f; // amplitude total (subida + descida)

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Eventos (plugue no Inspector)")]
    public UnityEvent onDie;                  // chamado ao morrer
    public UnityEvent<Vector2> onPlayerBounce;// passa a posição do inimigo

    // internos
    private float directionTimer;
    private int currentDirection = 1; // 1 = sobe, -1 = desce
    private float startY, minHeight, maxHeight;

    private Rigidbody2D rb;
    private Collider2D[] allColliders;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = false; // queremos girar na morte
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // enquanto vivo, comportamento de voo: movimento manual, sem gravidade
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        allColliders = GetComponentsInChildren<Collider2D>(true);
    }

    void Start()
    {
        CreateHeadTriggerIfNeeded();
        directionTimer = changeDirectionTime;

        // limites verticais a partir da posição inicial
        startY = transform.position.y;
        float half = Mathf.Max(0.01f, verticalDistance * 0.5f);
        minHeight = startY - half;
        maxHeight = startY + half;
    }

    void Update()
    {
        if (!isDead)
            MoveVertically();
    }

    // ----------------- movimento de voo -----------------
    void MoveVertically()
    {
        transform.Translate(Vector3.up * currentDirection * moveSpeed * Time.deltaTime);

        directionTimer -= Time.deltaTime;
        bool hitTop = transform.position.y >= maxHeight;
        bool hitBottom = transform.position.y <= minHeight;

        if (directionTimer <= 0f || hitTop || hitBottom)
        {
            currentDirection = (Random.Range(0, 2) == 0) ? 1 : -1;
            directionTimer = Random.Range(changeDirectionTime * 0.5f, changeDirectionTime * 1.5f);

            // mantém dentro dos limites
            float clamped = Mathf.Clamp(transform.position.y, minHeight, maxHeight);
            transform.position = new Vector3(transform.position.x, clamped, transform.position.z);
        }
    }

    // ----------------- trigger de cabeça -----------------
    void CreateHeadTriggerIfNeeded()
    {
        if (headTrigger != null) return;

        GameObject triggerObj = new GameObject("HeadTrigger");
        triggerObj.transform.SetParent(transform);
        triggerObj.transform.localPosition = new Vector3(0f, triggerHeight, 0f);

        headTrigger = triggerObj.AddComponent<BoxCollider2D>();
        headTrigger.isTrigger = true;
        headTrigger.size = triggerSize;

        // opcional: marcar tag para debug/filters
        triggerObj.tag = "EnemyHead";
    }

    // Como o Rigidbody2D está no objeto raiz, o OnTriggerEnter2D do PAI
    // é chamado quando o Player entra no trigger filho (headTrigger).
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (!other.CompareTag(playerTag)) return;

        // Precisa estar descendo para contar como stomp
        Rigidbody2D playerRb = other.attachedRigidbody ? other.attachedRigidbody : other.GetComponent<Rigidbody2D>();
        if (playerRb != null && playerRb.linearVelocity.y <= 0f)
        {
            Die();

            if (deathSound) deathSound.Play();
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);

            onPlayerBounce?.Invoke(transform.position);
        }
    }

    // Fallback: caso colida sólido sem passar pelo trigger (variações de setup)
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (!collision.collider.CompareTag(playerTag)) return;

        Rigidbody2D prb = collision.rigidbody;
        bool playerAbove = collision.transform.position.y > (transform.position.y + 0.05f);
        bool playerDescending = (prb != null && prb.linearVelocity.y <= 0f);

        if (playerAbove && playerDescending)
        {
            Die();

            if (deathSound) deathSound.Play();
            if (prb) prb.linearVelocity = new Vector2(prb.linearVelocity.x, stompBounce);

            onPlayerBounce?.Invoke(transform.position);
        }
    }

    // ----------------- morte/queda -----------------
    void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1) desativa colisores (vira fantasma)
        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
                if (allColliders[i]) allColliders[i].enabled = false;
        }

        // 2) solta a física e cai
        rb.constraints = RigidbodyConstraints2D.None;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = deathGravity;
        rb.linearVelocity = Vector2.zero;

        float dirKnock = (Random.value < 0.5f) ? -1f : 1f;
        rb.AddForce(new Vector2(dirKnock * deathKnockbackX, deathJumpY), ForceMode2D.Impulse);
        if (Mathf.Abs(deathTorque) > 0.01f)
            rb.AddTorque(deathTorque * -dirKnock, ForceMode2D.Impulse);

        // 3) animação opcional
        if (animator && !string.IsNullOrEmpty(deadTrigger))
            animator.SetTrigger(deadTrigger);

        // 4) eventos externos (pontuação, VFX etc.)
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

#if UNITY_EDITOR
    // ajuda a manter valores coerentes ao editar
    void OnValidate()
    {
        verticalDistance = Mathf.Max(0.1f, verticalDistance);
        changeDirectionTime = Mathf.Max(0.05f, changeDirectionTime);
        if (headTrigger)
        {
            headTrigger.isTrigger = true;
            headTrigger.size = triggerSize;
            headTrigger.transform.localPosition = new Vector3(0f, triggerHeight, 0f);
        }
    }
#endif
}
