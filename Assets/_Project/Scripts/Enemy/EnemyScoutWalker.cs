// Assets/_Project/Scripts/Enemies/EnemyScoutWalker.cs
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyScoutWalker : MonoBehaviour
{
    public enum State { Run, Look }
    public enum AutoDirMode { None, FromVisual }

    [Header("Movimento")]
    [SerializeField] float speed = 2.0f;
    [Tooltip("1 = direita, -1 = esquerda (usado se AutoDir = None)")]
    [SerializeField] int direction = -1;

    [Header("Direção Inicial")]
    [SerializeField] AutoDirMode autoDirection = AutoDirMode.FromVisual;
    [SerializeField] bool invertInitialDirection = false;

    [Header("Visual")]
    [SerializeField] bool invertFacing = true; // visual invertido vs movimento

    [Header("Detecção de borda/parede")]
    [SerializeField] Transform groundCheck;
    [SerializeField] Transform wallCheck;
    [SerializeField] float groundCheckDistance = 0.25f;
    [SerializeField] float wallCheckDistance   = 0.12f;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask wallMask;
    [SerializeField] float edgeProbeAhead = 0.1f;
    [SerializeField] float flipCooldown = 0.15f;

    [Header("Ciclo aleatório")]
    [SerializeField] Vector2 runTimeRange = new Vector2(1.2f, 2.4f);
    [SerializeField] Vector2 lookHoldRange = new Vector2(0.25f, 0.5f);
    [SerializeField] Vector2 lookIdleDelayRange = new Vector2(0.05f, 0.15f);

    [Header("Stomp (pisão do player)")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] float stompYOffset = 0.1f;
    [SerializeField] float stompBounce = 8f;

    [Header("Morte com pulo + queda")]
    [SerializeField] bool  destroyOnDeath  = true;
    [SerializeField] float deathJumpY      = 5.5f;
    [SerializeField] float deathKnockbackX = 1.5f;
    [SerializeField] float deathGravity    = 4.5f;
    [SerializeField] float deathTorque     = 25f;
    [SerializeField] float offscreenMargin = 1.0f;
    [SerializeField] float deathTimeout    = 4.0f;

    [Header("Dano no Player (lateral/baixo)")]
    [SerializeField] float hitCooldown = 0.35f;
    [SerializeField] float hitKnockbackX = 6f;

    [Header("Animação (opcional)")]
    [SerializeField] Animator animator;
    [SerializeField] string walkBool = "walk";
    [SerializeField] string lookBool = "look";
    [SerializeField] string deadTrigger = "dead";

    [Header("Áudio (opcional)")]
    public AudioSource deathSound;

    [Header("Eventos (opcionais)")]
    public UnityEvent onDie;
    public UnityEvent onPlayerHit;
    public UnityEvent<Vector2> onPlayerBounce;

    // ---- internos
    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    Collider2D[] allColliders;

    public State state { get; private set; } = State.Run;
    float stateTimer = 0f;
    float lastFlipTime = -999f;
    float lastHitTime  = -999f;
    bool  isDead = false;
    bool  lookStageTwo = false;
    int   cachedRunOppositeDir = 1;

    Vector3 groundChkLocal0, wallChkLocal0;

    // debounce/histerese de sensor
    int missGroundFrames = 0;
    int hitWallFrames = 0;
    [SerializeField] int sensorFramesNeeded = 2; // 2–3 frames é bom

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        allColliders = GetComponentsInChildren<Collider2D>();

        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground", "Piso");
        if (wallMask.value   == 0) wallMask   = groundLayer;

        ResolveInitialDirection();

        if (groundCheck) groundChkLocal0 = groundCheck.localPosition;
        if (wallCheck)   wallChkLocal0   = wallCheck.localPosition;
        UpdateSensorOffsets();

        EnterRun();
    }

    void Update()
    {
        if (isDead) return;

        // --- SENSORES COM DEBOUNCE ---
        bool noGround = !GroundAhead();
        bool wall     =  WallAhead();

        missGroundFrames = noGround ? missGroundFrames + 1 : 0;
        hitWallFrames    = wall     ? hitWallFrames + 1    : 0;

        bool wantFlip = (missGroundFrames >= sensorFramesNeeded) || (hitWallFrames >= sensorFramesNeeded);

        // Só flipa ao detectar obstáculo se estiver CORRENDO
        if (state == State.Run && wantFlip && Time.time - lastFlipTime > flipCooldown)
        {
            direction *= -1;
            lastFlipTime = Time.time;
            UpdateSensorOffsets();

            missGroundFrames = 0;
            hitWallFrames = 0;
        }

        // Se está olhando e detectou risco, encerra “look” e garante que irá correr pro lado seguro
        if (state == State.Look && wantFlip)
        {
            cachedRunOppositeDir = -direction;
            stateTimer = 0f; // força avançar ciclo
        }

        // --- MÁQUINA DE ESTADOS ---
        stateTimer -= Time.deltaTime;
        switch (state)
        {
            case State.Run:
                if (stateTimer <= 0f) EnterLook();
                break;
            case State.Look:
                if (stateTimer <= 0f) AdvanceLookStageOrRun();
                break;
        }

        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (isDead) return;
        rb.linearVelocity = (state == State.Run)
            ? new Vector2(direction * speed, rb.linearVelocity.y)
            : new Vector2(0f, rb.linearVelocity.y);
    }

    void LateUpdate()
    {
        if (isDead) return;
        if (sr)
        {
            // flip padrão (direction<0 => olhando à esquerda) XOR invertFacing
            sr.flipX = (direction < 0) ^ invertFacing;
        }
    }

    // ---------- direção inicial com invertFacing ----------
    void ResolveInitialDirection()
    {
        int dir = direction < 0 ? -1 : 1;

        if (autoDirection == AutoDirMode.FromVisual && sr)
        {
            int visualDir = sr.flipX ? -1 : 1;                   // -1 esquerda, +1 direita
            int signScale = Mathf.Sign(transform.lossyScale.x) >= 0 ? 1 : -1;
            visualDir *= signScale;

            if (invertFacing) visualDir *= -1; // movimento = oposto do "olhar"

            dir = visualDir;
        }

        if (invertInitialDirection) dir *= -1;
        direction = dir;
    }

    // ---------- estados ----------
    void EnterRun()
    {
        state = State.Run;
        lookStageTwo = false;
        stateTimer = Random.Range(runTimeRange.x, runTimeRange.y);
        if (animator)
        {
            if (!string.IsNullOrEmpty(walkBool)) animator.SetBool(walkBool, true);
            if (!string.IsNullOrEmpty(lookBool)) animator.SetBool(lookBool, false);
        }
    }

    void EnterLook()
    {
        state = State.Look;
        lookStageTwo = false;
        stateTimer = Random.Range(lookIdleDelayRange.x, lookIdleDelayRange.y)
                   + Random.Range(lookHoldRange.x, lookHoldRange.y);
        cachedRunOppositeDir = -direction;

        if (animator)
        {
            if (!string.IsNullOrEmpty(walkBool)) animator.SetBool(walkBool, false);
            if (!string.IsNullOrEmpty(lookBool)) animator.SetBool(lookBool, true);
        }
    }

    void AdvanceLookStageOrRun()
    {
        if (!lookStageTwo)
        {
            direction *= -1; // vira o rosto (sem mover)
            UpdateSensorOffsets();

            lookStageTwo = true;
            stateTimer = Random.Range(lookHoldRange.x, lookHoldRange.y);
        }
        else
        {
            direction = cachedRunOppositeDir; // corre pro lado oposto
            UpdateSensorOffsets();
            EnterRun();
        }
    }

    // ---------- sensores ----------
    bool GroundAhead()
    {
        Vector2 origin;
        if (groundCheck) origin = groundCheck.position;
        else
        {
            Bounds b = col.bounds;
            float ahead = Mathf.Sign(direction) * (b.extents.x + edgeProbeAhead);
            origin = new Vector2(b.center.x + ahead, b.min.y + 0.02f);
        }
        var hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        Debug.DrawLine(origin, origin + Vector2.down * groundCheckDistance, hit ? Color.yellow : Color.red, 0f);
        return hit.collider != null;
    }

    bool WallAhead()
    {
        Vector2 origin;
        if (wallCheck) origin = wallCheck.position;
        else
        {
            Bounds b = col.bounds;
            float ahead = Mathf.Sign(direction) * (b.extents.x + 0.02f);
            origin = new Vector2(b.center.x + ahead, b.center.y);
        }
        Vector2 lookDir = new Vector2(direction, 0f);
        var hit = Physics2D.Raycast(origin, lookDir, wallCheckDistance, wallMask);
        Debug.DrawLine(origin, origin + lookDir * wallCheckDistance, hit ? Color.cyan : Color.gray, 0f);
        if (!hit.collider) return false;
        float facing = Mathf.Sign(direction);
        return hit.normal.x * facing < -0.2f; // normal contra o movimento = parede
    }

    void UpdateAnimator()
    {
        if (!animator) return;
        if (!string.IsNullOrEmpty(walkBool))
            animator.SetBool(walkBool, state == State.Run && Mathf.Abs(rb.linearVelocity.x) > 0.01f);
        if (!string.IsNullOrEmpty(lookBool))
            animator.SetBool(lookBool, state == State.Look);
    }

    void UpdateSensorOffsets()
    {
        if (groundCheck)
            groundCheck.localPosition = new Vector3(Mathf.Abs(groundChkLocal0.x) * direction, groundChkLocal0.y, groundChkLocal0.z);
        if (wallCheck)
            wallCheck.localPosition   = new Vector3(Mathf.Abs(wallChkLocal0.x)   * direction, wallChkLocal0.y,   wallChkLocal0.z);
    }

    // ---------- combate / morte ----------
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (!collision.collider.CompareTag(playerTag)) return;

        var prb = collision.rigidbody;
        bool playerAbove      = collision.transform.position.y > (transform.position.y + stompYOffset);
        bool playerDescending = prb ? (prb.linearVelocity.y <= 0f) : true;

        if (playerAbove && playerDescending)
        {
            Die();
            if (deathSound) deathSound.Play();
            if (prb) prb.linearVelocity = new Vector2(prb.linearVelocity.x, stompBounce);
            onPlayerBounce?.Invoke(transform.position);
        }
        else
        {
            if (Time.time - lastHitTime >= hitCooldown)
            {
                lastHitTime = Time.time;
                if (prb)
                {
                    float dir = Mathf.Sign(prb.position.x - rb.position.x);
                    prb.linearVelocity = new Vector2(dir * hitKnockbackX, prb.linearVelocity.y);
                }
                onPlayerHit?.Invoke();
            }
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (allColliders != null)
            foreach (var c in allColliders) if (c) c.enabled = false;

        rb.constraints = RigidbodyConstraints2D.None;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = deathGravity;
        rb.linearVelocity = Vector2.zero;

        float dirKnock = (Random.value < 0.5f) ? -1f : 1f;
        rb.AddForce(new Vector2(dirKnock * deathKnockbackX, deathJumpY), ForceMode2D.Impulse);
        if (Mathf.Abs(deathTorque) > 0.01f) rb.AddTorque(deathTorque * -dirKnock, ForceMode2D.Impulse);

        if (animator && !string.IsNullOrEmpty(deadTrigger)) animator.SetTrigger(deadTrigger);
        onDie?.Invoke();

        if (destroyOnDeath) InvokeRepeating(nameof(CheckOffscreenAndDestroy), 0.15f, 0.15f);
        Destroy(gameObject, deathTimeout);
    }

    void CheckOffscreenAndDestroy()
    {
        var cam = Camera.main;
        if (!cam) { Destroy(gameObject); return; }
        float camBottom = cam.transform.position.y - cam.orthographicSize;
        if (transform.position.y < camBottom - offscreenMargin)
            Destroy(gameObject);
    }
}
