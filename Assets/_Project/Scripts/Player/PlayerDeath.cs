using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerDeath: mata o player quando:
/// 1) Colide com inimigo em qualquer região que NÃO seja a parte de baixo do player;
///    (se qualquer contato tiver normal.y > 0.5 consideramos toque pela base → NÃO mata)
/// 2) Entra numa KillZone (por Tag e/ou por Layer).
///
/// Após a morte:
/// - Desliga colliders (opcional)
/// - Troca de layer para "Dead" (opcional)
/// - Usa excludeLayers (opcional)
/// - Aplica knockback/torque/gravity
/// - Dispara animação e eventos
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDeath : MonoBehaviour
{
    [Header("Detecção de Morte")]
    [SerializeField] LayerMask enemyLayers = 0;
    [SerializeField] string   killZoneTag = "KillZone";
    [SerializeField] LayerMask killZoneLayers = 0;

    [Header("Física da morte")]
    [SerializeField] float deathGravity   = 4.5f;
    [SerializeField] float deathJumpY     = 5.5f;
    [SerializeField] float deathKnockbackX= 1.4f;
    [SerializeField] float deathTorque    = 20f;

    [Header("Colisão pós-morte (anti-bateção)")]
    [SerializeField] bool disableCollidersOnDeath = true;
    [SerializeField] bool changeLayerOnDeath = true;
    [SerializeField] string deadLayerName = "Dead";
    [SerializeField] bool useExcludeLayersOnDeath = true;
    [SerializeField] LayerMask ignoreOnDeath = 0;

    [Header("Animação/Áudio (opcional)")]
    [SerializeField] Animator animator;
    [SerializeField] string deadTrigger = "dead";
    public AudioSource deathSound;

    [Header("Eventos")]
    public UnityEvent onDie;
    public System.Action<PlayerDeath> OnDied;

    [Header("Visual Fantasma")]
    [SerializeField] Color deadGhostTint = new Color(0.85f, 0.95f, 1f, 0.45f);
    [SerializeField, Min(0f)] float ghostRiseSpeed = 1f;
    [SerializeField, Min(0.1f)] float ghostLifetime = 2f;
    [SerializeField] int ghostSortingOffset = 1;
    [SerializeField] bool spawnFloatingGhost = true;

    // ---- internos
    Rigidbody2D rb;
    Collider2D[] allColliders;
    SpriteRenderer spriteRenderer;
    int originalLayer;
    float originalGravityScale;
    RigidbodyConstraints2D originalConstraints;
    RigidbodyType2D originalBodyType;
    Color originalSpriteColor;

    bool isDead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (!animator) animator = GetComponent<Animator>();
        allColliders = GetComponentsInChildren<Collider2D>(true);
        originalLayer = gameObject.layer;
        originalGravityScale = rb.gravityScale;
        originalConstraints = rb.constraints;
        originalBodyType = rb.bodyType;
        if (spriteRenderer) originalSpriteColor = spriteRenderer.color;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (!IsInLayerMask(collision.collider.gameObject.layer, enemyLayers)) return;

        // contato pela base do player? (normal de contato apontando pra cima)
        bool contactFromBottom = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var n = collision.GetContact(i).normal;
            if (n.y > 0.5f) { contactFromBottom = true; break; }
        }
        if (!contactFromBottom) Kill();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (!string.IsNullOrEmpty(killZoneTag) && other.CompareTag(killZoneTag))
        { Kill(); return; }

        if (IsInLayerMask(other.gameObject.layer, killZoneLayers))
        { Kill(); return; }
    }

    public void Kill()
    {
        if (isDead) return;
        isDead = true;

        GameController.Instance?.HandlePlayerKilled(this);

        // parar input
        GetComponent<PlayerInput>()?.DeactivateInput();

        // desligar colisores
        if (disableCollidersOnDeath && allColliders != null)
            foreach (var c in allColliders) if (c) c.enabled = false;

        // layer
        if (changeLayerOnDeath)
        {
            int deadLayer = LayerMask.NameToLayer(deadLayerName);
            if (deadLayer >= 0) gameObject.layer = deadLayer;
        }

#if UNITY_2022_3_OR_NEWER || UNITY_6_0_OR_NEWER
        if (useExcludeLayersOnDeath) rb.excludeLayers |= ignoreOnDeath;
#endif

        // física da morte
        rb.constraints = RigidbodyConstraints2D.None;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = deathGravity;

        float dirKnock = (Random.value < 0.5f) ? -1f : 1f;
        rb.AddForce(new Vector2(dirKnock * deathKnockbackX, deathJumpY), ForceMode2D.Impulse);
        if (Mathf.Abs(deathTorque) > 0.01f) rb.AddTorque(deathTorque * -dirKnock, ForceMode2D.Impulse);

        // anima/áudio/eventos
        if (animator && !string.IsNullOrEmpty(deadTrigger)) animator.SetTrigger(deadTrigger);
        if (deathSound) deathSound.Play();

        onDie?.Invoke();
        OnDied?.Invoke(this);
    }

    public void RespawnAt(Transform spawn)
    {
        isDead = false;

        // layer original
        gameObject.layer = originalLayer;

        // reset físico
        rb.bodyType = originalBodyType;
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.rotation = 0f;
        rb.constraints = originalConstraints;

#if UNITY_2022_3_OR_NEWER || UNITY_6_0_OR_NEWER
        rb.excludeLayers = 0;
#endif

        // reativar colisores
        if (allColliders != null)
            foreach (var c in allColliders) if (c) c.enabled = true;

        // reposicionar
        if (spawn) transform.position = spawn.position;
        transform.rotation = Quaternion.identity;

        if (spriteRenderer)
            spriteRenderer.color = originalSpriteColor;

        // reset anima
        animator?.Rebind();
        animator?.Update(0f);

        // reativar input
        GetComponent<PlayerInput>()?.ActivateInput();
    }

    public void SpawnGhostVisual(Vector3? positionOverride = null)
    {
        if (!spawnFloatingGhost || !spriteRenderer) return;

        Color ghostColor = deadGhostTint;
        if (ghostColor.a <= 0f) ghostColor.a = 0.5f;

        var effect = GhostRiseEffect.SpawnFrom(spriteRenderer, ghostColor, ghostLifetime, ghostRiseSpeed, ghostSortingOffset);
        if (effect && positionOverride.HasValue)
            effect.transform.position = positionOverride.Value;
    }

    static bool IsInLayerMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
}
