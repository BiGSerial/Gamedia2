// Assets/_Project/Scripts/Camera/SideScrollCamera2D.cs
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SideScrollCamera2D : MonoBehaviour
{
    [Header("Alvo")]
    public Transform target;             // jogador
    public Rigidbody2D targetRb;         // opcional (lookahead)

    [Header("Dead Zone (unidades de mundo)")]
    public float deadZoneX = 1.5f;
    public float deadZoneY = 0.75f;

    [Header("Comportamento")]
    public bool lockVertical = false;    // gameplay normal: false
    [Range(0f, 20f)] public float lookAhead = 0f; // 0.3–0.6 mostra “à frente”
    [Range(0f, 10f)] public float smooth = 0f;    // 0 = sem suavização

    [Header("Offset (subir/baixar a câmera)")]
    public float verticalOffset = 0.5f;  // + sobe a câmera (mais céu), - desce

    [Header("Limites do Mundo")]
    public Collider2D worldBounds;       // Box/Composite com o retângulo do level

    [Header("Pixel Art")]
    public bool pixelSnap = true;
    public int ppu = 16;

    [Header("Debug/Proteção")]
    [SerializeField] bool hardLockWhileVerticalLocked = true; // garante travamento de Y

    Camera cam;
    float lockedBaseY;       // Y base quando lockVertical=true
    bool lockedYInitialized;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (!target) return;

        // ---- HARD LOCK: se travado, ignora qualquer cálculo de Y neste frame ----
        if (lockVertical && hardLockWhileVerticalLocked)
        {
            Vector3 camPos = transform.position;

            // X com deadzone (mantemos o comportamento padrão de X)
            float vx = targetRb ? targetRb.linearVelocity.x : 0f;
            float desiredX = target.position.x + Mathf.Sign(vx) * lookAhead;

            if (desiredX > camPos.x + deadZoneX)      camPos.x = desiredX - deadZoneX;
            else if (desiredX < camPos.x - deadZoneX) camPos.x = desiredX + deadZoneX;

            // Y fixo no pino + offset
            camPos.y = lockedBaseY + verticalOffset;

            // suavização opcional
            if (smooth > 0f)
                camPos = Vector3.Lerp(transform.position, camPos, 1f - Mathf.Exp(-smooth * Time.deltaTime));

            camPos = ClampToWorld(camPos);

            if (pixelSnap && ppu > 0)
            {
                float upp = 1f / ppu;
                camPos.x = Mathf.Round(camPos.x / upp) * upp;
                camPos.y = Mathf.Round(camPos.y / upp) * upp;
            }

            transform.position = camPos;
            return; // nada mais altera Y
        }

        // --------- fluxo normal ----------
        float vx2 = targetRb ? targetRb.linearVelocity.x : 0f;
        Vector3 desired = new Vector3(
            target.position.x + Mathf.Sign(vx2) * lookAhead,
            target.position.y,
            transform.position.z
        );

        Vector3 camPos2 = transform.position;

        // X
        if (desired.x > camPos2.x + deadZoneX)      camPos2.x = desired.x - deadZoneX;
        else if (desired.x < camPos2.x - deadZoneX) camPos2.x = desired.x + deadZoneX;

        // Y
        if (!lockVertical)
        {
            if (desired.y > camPos2.y + deadZoneY)      camPos2.y = desired.y - deadZoneY;
            else if (desired.y < camPos2.y - deadZoneY) camPos2.y = desired.y + deadZoneY;
        }
        else
        {
            if (!lockedYInitialized)
            {
                lockedBaseY = camPos2.y;   // memoriza o Y atual como base
                lockedYInitialized = true;
            }
            camPos2.y = lockedBaseY;       // trava o Y
        }

        // offset
        camPos2.y += verticalOffset;

        // suavização
        if (smooth > 0f)
            camPos2 = Vector3.Lerp(transform.position, camPos2, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        // clamp
        camPos2 = ClampToWorld(camPos2);

        // pixel snap
        if (pixelSnap && ppu > 0)
        {
            float upp = 1f / ppu;
            camPos2.x = Mathf.Round(camPos2.x / upp) * upp;
            camPos2.y = Mathf.Round(camPos2.y / upp) * upp;
        }

        transform.position = camPos2;
    }

    Vector3 ClampToWorld(Vector3 pos)
    {
        if (!worldBounds) return pos;

        Bounds b = worldBounds.bounds;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        pos.x = Mathf.Clamp(pos.x, b.min.x + halfW, b.max.x - halfW);
        pos.y = Mathf.Clamp(pos.y, b.min.y + halfH, b.max.y - halfH);

        return pos;
    }

    void OnDrawGizmosSelected()
    {
        if (!cam) cam = GetComponent<Camera>();
        Gizmos.color = new Color(0f, 1f, 0f, 0.35f);

        float halfH = cam ? cam.orthographicSize : 5f;
        float halfW = halfH * (cam ? cam.aspect : 16f / 9f);
        Vector3 c = transform.position;

        float dzW = Mathf.Max(0.01f, (halfW - deadZoneX) * 2f);
        float dzH = lockVertical ? 0.01f : Mathf.Max(0.01f, (halfH - deadZoneY) * 2f);

        Gizmos.DrawWireCube(new Vector3(c.x, c.y, 0f), new Vector3(dzW, dzH, 0.01f));
    }

    // ==== API pública de travamento ====
    public void SetLockVertical(bool locked, bool pinAtCurrent = false)
    {
        lockVertical = locked;

        if (locked)
        {
            if (pinAtCurrent)
            {
                // Em LateUpdate, o offset é aplicado DEPOIS do lock.
                // Para manter a posição visual atual, subtraímos o offset aqui.
                lockedBaseY = transform.position.y - verticalOffset;
                lockedYInitialized = true;   // já pinado
            }
            else
            {
                lockedYInitialized = false;  // recalcula no próximo LateUpdate
            }
        }
    }

    public void LockVerticalAtCurrentY()  => SetLockVertical(true,  true);
    public void UnlockVertical()          => SetLockVertical(false, false);
}
