using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyChaoticFlyer : MonoBehaviour
{
    public enum State { Ascend, Fall, PanicAscend, GroundWait }

    [Header("Altura padrão (baseline)")]
    [Tooltip("Largura da faixa de tolerância ao estabilizar na baseline.")]
    [SerializeField] float baselineSettleBand = 0.15f; // ~ quão perto da baseline consideramos “ok”

    [Header("Alvos de subida (random)")]
    [Tooltip("Altura mínima/máxima acima da baseline para os picos normais.")]
    [SerializeField] Vector2 hoverHeightRange = new Vector2(1.6f, 3.0f);
    [Tooltip("Limite acima da baseline; se ultrapassar, força queda (Fall).")]
    [SerializeField] float maxAboveBaseline = 4.5f;
    [Tooltip("Chance (0–1) de escolher ocasionalmente um alvo acima do limite (vai despencar).")]
    [Range(0f, 1f)] [SerializeField] float overMaxChance = 0.25f;
    [Tooltip("Altura do alvo ‘exagerado’ acima da baseline.")]
    [SerializeField] float overMaxHeight = 6.0f;

    [Header("Forças/Velocidades")]
    [Tooltip("Aceleração para cima nas subidas normais.")]
    [SerializeField] float ascendAcceleration = 22f;
    [Tooltip("Velocidade vertical máxima ao subir normalmente.")]
    [SerializeField] float maxAscendSpeed = 6.5f;
    [Tooltip("Aceleração para cima no modo pânico (próximo do fundo).")]
    [SerializeField] float panicAscendAcceleration = 32f;
    [Tooltip("Velocidade vertical máxima no pânico.")]
    [SerializeField] float maxPanicAscendSpeed = 9.5f;
    [Tooltip("Gravidade enquanto vivo (cai de verdade).")]
    [SerializeField] float aliveGravity = 3.6f;
    [Tooltip("Arrasto leve para suavizar picos.")]
    [SerializeField] float linearDragWhileAlive = 0.4f;

    [Header("Tela / Segurança (bottom)")]
    [Tooltip("Margem acima da borda inferior da câmera que consideramos ‘perigo’.")]
    [SerializeField] float bottomScreenMargin = 0.35f;
    [Tooltip("Faixa acima do limite em que entramos em Pânico.")]
    [SerializeField] float panicBand = 0.5f;

    [Header("Chão (quique/espera)")]
    [SerializeField] LayerMask groundLayer;
    [Tooltip("Força do quique ao bater no chão durante a queda.")]
    [SerializeField] float groundBounceForce = 6.5f;
    [Tooltip("Janela aleatória de espera no chão antes de voltar a voar.")]
    [SerializeField] Vector2 groundWaitSeconds = new Vector2(3f, 8f);

    [Header("Animator (parâmetros)")]
    [SerializeField] Animator animator;
    [Tooltip("Trigger disparado quando inicia/retoma subida.")]
    [SerializeField] string flyTrigger = "Fly";
    [Tooltip("Trigger disparado quando entra em queda (gravidade).")]
    [SerializeField] string fallTrigger = "Fall";
    [Tooltip("Bool que indica se está apoiado no chão.")]
    [SerializeField] string groundBool = "Ground";
    [Header("Animator (velocidades)")]
    [SerializeField] float animSpeedAscend = 1.0f;    // cruzeiro
    [SerializeField] float animSpeedPanic  = 1.35f;   // acelerada
    [SerializeField] float animSpeedFall   = 1.2f;    // queda

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
    public UnityEvent<Vector2> onPlayerBounce;

    // ---- internos
    Rigidbody2D rb;
    Collider2D  col;
    Camera cam;

    State state = State.Ascend;
    bool isDead = false;

    float baselineY;     // altura padrão (spawn)
    float targetTopY;    // topo atual
    float minSafeY;      // y mínimo seguro (baseado na câmera)
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
    }

    void Start()
    {
        baselineY = transform.position.y; // nasce = baseline
        PickNewTargetTop();               // define primeiro alvo

        UpdateMinSafeYFromCamera(true);

        EnterAscend(panic:false);
    }

    void Update()
    {
        if (isDead) return;

        UpdateMinSafeYFromCamera();

        // Se está caindo e entrou em zona de pânico (perto do fundo) → PanicAscend
        if (state == State.Fall)
        {
            float panicThreshold = minSafeY + bottomScreenMargin + panicBand;
            if (rb.position.y <= panicThreshold)
            {
                EnterPanicAscend();
            }
        }

        // Se está esperando no chão, conta tempo
        if (state == State.GroundWait)
        {
            groundWaitTimer -= Time.deltaTime;
            if (groundWaitTimer <= 0f)
            {
                // Levanta em pânico até a baseline e volta ao ciclo
                EnterPanicAscend();
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        // clamp de segurança: nunca descer abaixo do mínimo visível - margem
        float camBottom = cam ? cam.transform.position.y - cam.orthographicSize : -99999f;
        float hardBottom = camBottom - 0.25f; // um pouquinho abaixo para não travar em borda
        if (rb.position.y < hardBottom)
        {
            rb.position = new Vector2(rb.position.x, hardBottom);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, 0f));
        }

        switch (state)
        {
            case State.Ascend:
{
    // Subida controlada
    float vy = Mathf.Min(rb.linearVelocity.y + ascendAcceleration * Time.fixedDeltaTime, maxAscendSpeed);
    rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy);

    // Regra de queda:
    // - MODO NORMAL: cai ao atingir o alvo sorteado (abaixo do máximo).
    // - MODO OVER: cai ao cruzar o limite "maxAboveBaseline".
    float topLimit = baselineY + maxAboveBaseline;

    if (currentTargetIsOverMax)
    {
        if (rb.position.y >= topLimit - EPS_Y)
            EnterFall(); // despenca ao atingir o máximo configurado
    }
    else
    {
        if (rb.position.y >= targetTopY - EPS_Y)
            EnterFall(); // cai no alvo normal (hover)
    }
    break;
}

            case State.Fall:
            {
                // queda natural (gravidade). Se bater no chão, OnCollisionEnter2D trata.
                // se aproximar do bottom, Update() joga p/ PanicAscend.
                break;
            }
            case State.PanicAscend:
            {
                // sobe mais forte até pelo menos a baseline; estabiliza na baseline
                bool reachedBaseline = rb.position.y >= (baselineY - baselineSettleBand);
                float targetY = Mathf.Max(baselineY, targetTopY); // garante que vamos acima da baseline

                float vy = Mathf.Min(rb.linearVelocity.y + panicAscendAcceleration * Time.fixedDeltaTime, maxPanicAscendSpeed);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy);

                if (reachedBaseline)
                {
                    // estabiliza: normaliza animação e volta ao ciclo com novo alvo
                    PickNewTargetTop();
                    EnterAscend(panic:false);
                }
                break;
            }
            case State.GroundWait:
            {
                // parado no chão: travamos vel. vertical
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                break;
            }
        }
    }

    // ----------------- Entradas de estado -----------------
    void EnterAscend(bool panic)
    {
        state = State.Ascend;

        // velocidade vertical mínima de arranque
        if (panic && rb.linearVelocity.y < 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        // Animator
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
        // impulso imediato: se estamos com velocidade negativa, anula para reagir rápido
        if (rb.linearVelocity.y < 0f) rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        if (animator)
        {
            if (!string.IsNullOrEmpty(groundBool)) animator.SetBool(groundBool, false);
            if (!string.IsNullOrEmpty(fallTrigger)) animator.ResetTrigger(fallTrigger);
            if (!string.IsNullOrEmpty(flyTrigger))  animator.SetTrigger(flyTrigger);
            animator.speed = animSpeedPanic; // voo “desesperado”
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
            // deixa a animação “parada” no chão (speed = 1 ou menos, a gosto)
            animator.speed = 1f;
        }
    }

    // ----------------- Alvos / câmera -----------------
    void PickNewTargetTop()
    {
       
        currentTargetIsOverMax = (Random.value < overMaxChance);

        if (currentTargetIsOverMax)
        {
           
            targetTopY = baselineY + overMaxHeight;
        }
        else
        {
            // alvo dentro do range normal de hover
            float h = Random.Range(hoverHeightRange.x, hoverHeightRange.y);
            targetTopY = baselineY + h;
        }

        // NADA de cap aqui — deixe o "over" realmente acima do max.
        // Segurança mínima opcional:
        // targetTopY = Mathf.Max(baselineY + 0.1f, targetTopY);
    }

    void UpdateMinSafeYFromCamera(bool force = false)
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        if (force || !Mathf.Approximately(lastCamY, cam.transform.position.y))
        {
            lastCamY = cam.transform.position.y;
            float camBottom = cam.transform.position.y - cam.orthographicSize;
            minSafeY = camBottom; // usamos a margin + panicBand nos checks
        }
    }

    // ----------------- Colisões / chão -----------------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        // bateu no chão durante a queda → quique + espera
        if (((1 << collision.collider.gameObject.layer) & groundLayer.value) != 0)
        {
            // só tratamos se está caindo ou muito baixo
            if (state == State.Fall)
            {
                // quique
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, groundBounceForce);
                // imediatamente entra em GroundWait após tocar (curto “quique” visual)
                EnterGroundWait();
            }
        }

        // stomp opcional (se quiser matar o inimigo por cima)
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

    // ----------------- Morte (opcional) -----------------
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        rb.gravityScale = deathGravity;
        rb.linearDamping = 0f;

        float dirKnock = (Random.value < 0.5f) ? -1f : 1f;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(dirKnock * deathKnockbackX, deathJumpY), ForceMode2D.Impulse);
        if (Mathf.Abs(deathTorque) > 0.01f) rb.AddTorque(deathTorque * -dirKnock, ForceMode2D.Impulse);

        if (animator)
        {
            if (!string.IsNullOrEmpty(fallTrigger)) animator.ResetTrigger(fallTrigger);
            if (!string.IsNullOrEmpty(flyTrigger))  animator.ResetTrigger(flyTrigger);
            if (!string.IsNullOrEmpty(groundBool))  animator.SetBool(groundBool, false);
            animator.speed = 1f;
        }

        onDie?.Invoke();

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
        // baseline e limites
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        float baseY = Application.isPlaying ? baselineY : transform.position.y;
        Gizmos.DrawLine(new Vector3(transform.position.x - 0.5f, baseY, 0f),
                        new Vector3(transform.position.x + 0.5f, baseY, 0f));
        // faixa normal
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        Gizmos.DrawLine(new Vector3(transform.position.x, baseY + hoverHeightRange.x, 0f),
                        new Vector3(transform.position.x, baseY + hoverHeightRange.y, 0f));
        // overMax
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
        Gizmos.DrawLine(new Vector3(transform.position.x, baseY + maxAboveBaseline, 0f),
                        new Vector3(transform.position.x, baseY + overMaxHeight, 0f));
    }
}
