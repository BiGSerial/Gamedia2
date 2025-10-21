using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyChaoticFlyer : MonoBehaviour
{
    public enum State { Ascend, Fall, PanicAscend, GroundWait }

    [Header("Altura padrão (baseline)")]
    [SerializeField] float baselineSettleBand = 0.15f;

    [Header("Alvos de subida (random)")]
    [SerializeField] Vector2 hoverHeightRange = new Vector2(1.6f, 3.0f);
    [SerializeField] float maxAboveBaseline = 4.5f;
    [Range(0f, 1f)] [SerializeField] float overMaxChance = 0.25f;
    [SerializeField] float overMaxHeight = 6.0f;

    [Header("Forças/Velocidades")]
    [SerializeField] float ascendAcceleration = 22f;
    [SerializeField] float maxAscendSpeed = 6.5f;
    [SerializeField] float panicAscendAcceleration = 32f;
    [SerializeField] float maxPanicAscendSpeed = 9.5f;
    [SerializeField] float aliveGravity = 3.6f;
    [SerializeField] float linearDragWhileAlive = 0.4f;

    [Header("Tela / Segurança (bottom)")]
    [SerializeField] float bottomScreenMargin = 0.35f;
    [SerializeField] float panicBand = 0.5f;

    [Header("Chão (quique/espera)")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundBounceForce = 6.5f;
    [SerializeField] Vector2 groundWaitSeconds = new Vector2(3f, 8f);

    [Header("Animator (parâmetros)")]
    [SerializeField] Animator animator;
    [SerializeField] string flyTrigger = "Fly";
    [SerializeField] string fallTrigger = "Fall";
    [SerializeField] string groundBool = "Ground";
    [Header("Animator (velocidades)")]
    [SerializeField] float animSpeedAscend = 1.0f;
    [SerializeField] float animSpeedPanic  = 1.35f;
    [SerializeField] float animSpeedFall   = 1.2f;

    [Header("Stomp/Morte (opcionais)")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] float stompYOffset = 0.1f;
    [SerializeField] float stompBounce  = 8f;
    [SerializeField] bool  destroyOnDeath  = true;
    [SerializeField] float deathJumpY      = 5.5f;
    [SerializeField] float deathKnockbackX = 1.0f;
    [SerializeField] float deathGravity    = 4.5f;
    [SerializeField] float deathTorque     = 20f;
    [SerializeField] float offscreenMargin = 1.0f;
    [SerializeField] float deathTimeout    = 4.0f;
    public AudioSource deathSound;
    public UnityEvent onDie;
    [Header("Pontuacao")]
    [SerializeField] int scoreValue = 250;
    public UnityEvent<Vector2> onPlayerBounce;

    // === NOVO: política de colisão ao morrer ===
    [Header("Death Collision Ignore")]
    [Tooltip("Desabilita TODOS os colliders ao morrer (recomendado)")]
    [SerializeField] bool disableCollidersOnDeath = true;

    [Tooltip("Troca a layer ao morrer (configure a layer 'Dead' na Collision Matrix)")]
    [SerializeField] bool changeLayerOnDeath = true;

    [Tooltip("Nome da layer usada após a morte")]
    [SerializeField] string deadLayerName = "Dead";

    [Tooltip("Além de layer/colliders, usa excludeLayers do Rigidbody2D")]
    [SerializeField] bool useExcludeLayersOnDeath = true;

    [Tooltip("Layers a ignorar após a morte (ex.: Ground/Tilemap/Items)")]
    [SerializeField] LayerMask ignoreOnDeath = 0;

    // ---- internos
    Rigidbody2D rb;
    Collider2D  col;
    Collider2D[] allColliders;
    int originalLayer;

    Camera cam;
    State state = State.Ascend;
    bool isDead = false;

    float baselineY;
    float targetTopY;
    float minSafeY;
    float lastCamY;
    float groundWaitTimer = 0f;
    bool  currentTargetIsOverMax = false;

    const float EPS_Y = 0.02f;

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        cam = Camera.main;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = aliveGravity;
        rb.linearDamping = linearDragWhileAlive;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        allColliders = GetComponentsInChildren<Collider2D>(true);
        originalLayer = gameObject.layer;
    }

    void Start()
    {
        baselineY = transform.position.y;
        PickNewTargetTop();
        UpdateMinSafeYFromCamera(true);
        EnterAscend(panic:false);
    }

    void Update()
    {
        if (isDead) return;

        UpdateMinSafeYFromCamera();

        if (state == State.Fall)
        {
            float panicThreshold = minSafeY + bottomScreenMargin + panicBand;
            if (rb.position.y <= panicThreshold)
                EnterPanicAscend();
        }

        if (state == State.GroundWait)
        {
            groundWaitTimer -= Time.deltaTime;
            if (groundWaitTimer <= 0f)
                EnterPanicAscend();
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        float camBottom = cam ? cam.transform.position.y - cam.orthographicSize : -99999f;
        float hardBottom = camBottom - 0.25f;
        if (rb.position.y < hardBottom)
        {
            rb.position = new Vector2(rb.position.x, hardBottom);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, 0f));
        }

        switch (state)
        {
            case State.Ascend:
            {
                float vy = Mathf.Min(rb.linearVelocity.y + ascendAcceleration * Time.fixedDeltaTime, maxAscendSpeed);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy);

                float topLimit = baselineY + maxAboveBaseline;
                if (currentTargetIsOverMax)
                {
                    if (rb.position.y >= topLimit - EPS_Y) EnterFall();
                }
                else
                {
                    if (rb.position.y >= targetTopY - EPS_Y) EnterFall();
                }
                break;
            }
            case State.Fall:
            {
                break;
            }
            case State.PanicAscend:
            {
                if (rb.linearVelocity.y < 0f)
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

                float vy = Mathf.Min(rb.linearVelocity.y + panicAscendAcceleration * Time.fixedDeltaTime, maxPanicAscendSpeed);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy);

                bool reachedBaseline = rb.position.y >= (baselineY - baselineSettleBand);
                if (reachedBaseline)
                {
                    PickNewTargetTop();
                    EnterAscend(panic:false);
                }
                break;
            }
            case State.GroundWait:
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                break;
            }
        }
    }

    // ----------------- Entradas de estado -----------------
    void EnterAscend(bool panic)
    {
        state = State.Ascend;
        if (panic && rb.linearVelocity.y < 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        if (animator)
        {
            if (!string.IsNullOrEmpty(groundBool)) animator.SetBool(groundBool, false);
            if (!string.IsNullOrEmpty(fallTrigger)) animator.ResetTrigger(fallTrigger);
            if (!string.IsNullOrEmpty(flyTrigger))  animator.SetTrigger(flyTrigger);
            animator.speed = animSpeedAscend;
        }
    }

    void EnterPanicAscend()
    {
        state = State.PanicAscend;
        if (rb.linearVelocity.y < 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        if (animator)
        {
            if (!string.IsNullOrEmpty(groundBool)) animator.SetBool(groundBool, false);
            if (!string.IsNullOrEmpty(fallTrigger)) animator.ResetTrigger(fallTrigger);
            if (!string.IsNullOrEmpty(flyTrigger))  animator.SetTrigger(flyTrigger);
            animator.speed = animSpeedPanic;
        }
    }

    void EnterFall()
    {
        state = State.Fall;
        if (animator)
        {
            if (!string.IsNullOrEmpty(groundBool)) animator.SetBool(groundBool, false);
            if (!string.IsNullOrEmpty(flyTrigger))  animator.ResetTrigger(flyTrigger);
            if (!string.IsNullOrEmpty(fallTrigger)) animator.SetTrigger(fallTrigger);
            animator.speed = animSpeedFall;
        }
    }

    void EnterGroundWait()
    {
        state = State.GroundWait;
        groundWaitTimer = Random.Range(groundWaitSeconds.x, groundWaitSeconds.y);

        if (animator)
        {
            if (!string.IsNullOrEmpty(groundBool)) animator.SetBool(groundBool, true);
            animator.speed = 1f;
        }
    }

    // ----------------- Alvos / câmera -----------------
    void PickNewTargetTop()
    {
        currentTargetIsOverMax = (Random.value < overMaxChance);
        if (currentTargetIsOverMax)
            targetTopY = baselineY + overMaxHeight;
        else
            targetTopY = baselineY + Random.Range(hoverHeightRange.x, hoverHeightRange.y);
    }

    void UpdateMinSafeYFromCamera(bool force = false)
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        if (force || !Mathf.Approximately(lastCamY, cam.transform.position.y))
        {
            lastCamY = cam.transform.position.y;
            float camBottom = cam.transform.position.y - cam.orthographicSize;
            minSafeY = camBottom;
        }
    }

    // ----------------- Colisões / chão -----------------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        // chão: quique + espera
        if (((1 << collision.collider.gameObject.layer) & groundLayer.value) != 0)
        {
            if (state == State.Fall)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, groundBounceForce);
                EnterGroundWait();
            }
        }

        // stomp opcional
        if (collision.collider.CompareTag(playerTag))
        {
            var prb = collision.rigidbody;
            bool playerAbove = collision.transform.position.y > (transform.position.y + stompYOffset);
            bool playerDescending = prb ? (prb.linearVelocity.y <= 0f) : true;
            if (playerAbove && playerDescending)
            {
                Die();
                if (deathSound) deathSound.Play();
                if (prb) prb.linearVelocity = new Vector2(prb.linearVelocity.x, stompBounce);
                onPlayerBounce?.Invoke(transform.position);
            }
        }
    }

    // ----------------- Morte (sem colisões como no EnemyWalker) -----------------
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1) Desabilita colisores (opção mais segura)
        if (disableCollidersOnDeath && allColliders != null)
        {
            foreach (var c in allColliders) if (c) c.enabled = false;
        }

        // 2) Física da morte (knock + torque)
        rb.gravityScale = deathGravity;
        rb.linearDamping = 0f;
        rb.linearVelocity = Vector2.zero;
        float dirKnock = (Random.value < 0.5f) ? -1f : 1f;
        rb.AddForce(new Vector2(dirKnock * deathKnockbackX, deathJumpY), ForceMode2D.Impulse);
        if (Mathf.Abs(deathTorque) > 0.01f)
            rb.AddTorque(deathTorque * -dirKnock, ForceMode2D.Impulse);

        // 3) Troca de layer para ignorar colisões
        if (changeLayerOnDeath)
        {
            int deadLayer = LayerMask.NameToLayer(deadLayerName);
            if (deadLayer >= 0) gameObject.layer = deadLayer;
        }

        // 4) Ignora layers via Rigidbody2D (reforço, Unity recentes)
        #if UNITY_2022_3_OR_NEWER || UNITY_6_0_OR_NEWER
        if (useExcludeLayersOnDeath)
        {
            rb.excludeLayers |= ignoreOnDeath;
        }
        #endif

        // 5) Animação/Evento
        if (animator)
        {
            if (!string.IsNullOrEmpty(fallTrigger)) animator.ResetTrigger(fallTrigger);
            if (!string.IsNullOrEmpty(flyTrigger))  animator.ResetTrigger(flyTrigger);
            if (!string.IsNullOrEmpty(groundBool))  animator.SetBool(groundBool, false);
            animator.speed = 1f;
        }
        onDie?.Invoke();
        if (deathSound) deathSound.Play();
        if (GameController.Instance)
            GameController.Instance.RegisterEnemyDefeated(scoreValue);

        // 6) Auto-destruição quando sair da tela (ou por timeout)
        if (destroyOnDeath) InvokeRepeating(nameof(CheckOffscreenAndDestroy), 0.15f, 0.15f);
        Destroy(gameObject, deathTimeout);
    }

    void CheckOffscreenAndDestroy()
    {
        if (!cam) cam = Camera.main;
        if (!cam) { Destroy(gameObject); return; }
        float camBottom = cam.transform.position.y - cam.orthographicSize;
        if (transform.position.y < camBottom - offscreenMargin)
            Destroy(gameObject);
    }

    // ----------------- Gizmos -----------------
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        float baseY = Application.isPlaying ? baselineY : transform.position.y;
        Gizmos.DrawLine(new Vector3(transform.position.x - 0.5f, baseY, 0f),
                        new Vector3(transform.position.x + 0.5f, baseY, 0f));

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        Gizmos.DrawLine(new Vector3(transform.position.x, baseY + hoverHeightRange.x, 0f),
                        new Vector3(transform.position.x, baseY + hoverHeightRange.y, 0f));

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
        Gizmos.DrawLine(new Vector3(transform.position.x, baseY + maxAboveBaseline, 0f),
                        new Vector3(transform.position.x, baseY + overMaxHeight, 0f));
    }
}
