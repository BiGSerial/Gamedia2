using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BoxHit : MonoBehaviour
{
    public enum HitMode { None, Bounce, Break }

    [Header("Quem pode bater")]
    public LayerMask hitterMask = 0;           // selecione a layer do Player

    [Header("Ação por lado")]
    public HitMode onHitTop    = HitMode.None;   // player vindo de cima
    public HitMode onHitBottom = HitMode.Bounce; // (tipo Mario: de baixo)
    public HitMode onHitLeft   = HitMode.None;
    public HitMode onHitRight  = HitMode.None;

    [Header("Bounce")]
    [Tooltip("Deslocamento em unidades de mundo (1u=1 tile se PPU=16)")]
    public float bounceDistance = 0.12f;
    [Tooltip("Tempo total do ida-e-volta")]
    public float bounceDuration = 0.12f;

    [Header("Drop (apenas quando Break)")]
    public GameObject dropPrefab;         // arraste sua fruta aqui
    public Transform  dropSpawnPoint;     // onde spawna (se vazio: em cima da caixa)
    public float      dropSpawnYOffset = 0.5f;

    [Header("Objeto associado (opcional)")]
    [Tooltip("Objeto que será ativado na quebra (ex.: efeito, peça visual).")]
    public GameObject associatedObjectToEnable;

    [Header("Animação (opcional)")]
    public Animator animator;
    public string hitTrigger   = "Hit";
    public string breakTrigger = "Break";

    [Header("Outros")]
    public float minHitSpeed = 0.1f;  // ignora toques muito fracos

    Collider2D col;
    bool broken;
    bool animating;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (!animator) animator = GetComponent<Animator>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // filtra quem pode bater
        if ((hitterMask.value & (1 << collision.collider.gameObject.layer)) == 0) return;

        // tenta pegar a velocidade do hitter (se tiver RB)
        float hitterSpeed = 0f;
        var rb = collision.rigidbody;
        if (rb) hitterSpeed = rb.linearVelocity.magnitude;
        if (hitterSpeed < minHitSpeed) return;

        // qual lado bateu?
        var side = DetectSide(collision);

        // decide o modo
        HitMode mode = HitMode.None;
        switch (side)
        {
            case HitSide.Top:    mode = onHitTop;    break;
            case HitSide.Bottom: mode = onHitBottom; break;
            case HitSide.Left:   mode = onHitLeft;   break;
            case HitSide.Right:  mode = onHitRight;  break;
        }

        if (mode == HitMode.None || broken) return;

        if (mode == HitMode.Bounce)
        {
            if (!animating) StartCoroutine(BounceRoutine(side));
            if (animator) animator.SetTrigger(hitTrigger);
        }
        else if (mode == HitMode.Break)
        {
            DoBreak();
        }
    }

    // ----------------- lógica de lados -----------------
    enum HitSide { Top, Bottom, Left, Right }

    HitSide DetectSide(Collision2D c)
    {
        // usa o contato de maior magnitude normal
        ContactPoint2D cp = c.GetContact(0);
        Vector2 n = cp.normal.normalized;

        // normal aponta PARA o hitter (do ponto de vista da caixa)
        // n.y >  0 => hitter abaixo empurrando para cima (acertou por baixo)  -> Bottom
        // n.y <  0 => hitter acima  empurrando para baixo (acertou por cima)  -> Top
        // n.x >  0 => hitter à esquerda empurrando pra direita -> Left
        // n.x <  0 => hitter à direita empurrando pra esquerda -> Right
        if (Mathf.Abs(n.y) >= Mathf.Abs(n.x))
            return (n.y > 0f) ? HitSide.Bottom : HitSide.Top;
        else
            return (n.x > 0f) ? HitSide.Left   : HitSide.Right;
    }

    // ----------------- efeitos -----------------

    IEnumerator BounceRoutine(HitSide side)
    {
        animating = true;

        Vector3 dir = Vector3.zero;
        switch (side)
        {
            case HitSide.Top:    dir = Vector3.up;    break;
            case HitSide.Bottom: dir = Vector3.down;  break;
            case HitSide.Left:   dir = Vector3.left;  break;
            case HitSide.Right:  dir = Vector3.right; break;
        }

        Vector3 start = transform.localPosition;
        Vector3 peak  = start + dir * bounceDistance;

        float half = Mathf.Max(0.01f, bounceDuration * 0.5f);
        float t = 0f;

        // ida
        while (t < half)
        {
            t += Time.deltaTime;
            float a = t / half;
            transform.localPosition = Vector3.Lerp(start, peak, a);
            yield return null;
        }

        // volta
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float a = t / half;
            transform.localPosition = Vector3.Lerp(peak, start, a);
            yield return null;
        }
        transform.localPosition = start;

        animating = false;
    }

    void DoBreak()
    {
        if (broken) return;
        broken = true;

        if (animator) animator.SetTrigger(breakTrigger);

        // desativa o collider para não reagir mais
        if (col) col.enabled = false;

        // ativa objeto associado (se houver)
        if (associatedObjectToEnable) associatedObjectToEnable.SetActive(true);

        // instancia o drop (fruta) – só na quebra
        if (dropPrefab)
        {
            Vector3 spawnPos = dropSpawnPoint
                ? dropSpawnPoint.position
                : transform.position + Vector3.up * dropSpawnYOffset;

            GameObject go = Instantiate(dropPrefab, spawnPos, Quaternion.identity);

            // se tiver RB2D, ele cai; se não tiver, adiciona
            var rb2d = go.GetComponent<Rigidbody2D>();
            if (!rb2d) rb2d = go.AddComponent<Rigidbody2D>();
            rb2d.gravityScale = rb2d.gravityScale == 0 ? 1f : rb2d.gravityScale;
        }

        // opcional: destruir o objeto após a anima (se sua anima chama um evento)
        // Destroy(gameObject, 0.1f);
    }
}
