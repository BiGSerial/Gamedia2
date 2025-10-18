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
    public bool lockVertical = true;     // tipo Mario (Y quase fixo)
    [Range(0f, 20f)] public float lookAhead = 0f; // 0.3–0.6 mostra “à frente”
    [Range(0f, 10f)] public float smooth = 0f;    // 0 = sem suavização

    [Header("Offset (subir/baixar a câmera)")]
    public float verticalOffset = 0.5f;  // + sobe a câmera (mais céu), - desce

    [Header("Limites do Mundo")]
    public Collider2D worldBounds;       // Box/Composite com o retângulo do level

    [Header("Pixel Art")]
    public bool pixelSnap = true;
    public int ppu = 16;

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

        // posição desejada baseada no alvo + lookahead
        float vx = targetRb ? targetRb.linearVelocity.x : 0f;  // <<< aqui é velocity
        Vector3 desired = new Vector3(
            target.position.x + Mathf.Sign(vx) * lookAhead,
            target.position.y,
            transform.position.z
        );

        // aplica dead zone (em torno da posição atual da câmera, NÃO do alvo)
        Vector3 camPos = transform.position;

        // X
        if (desired.x > camPos.x + deadZoneX) camPos.x = desired.x - deadZoneX;
        else if (desired.x < camPos.x - deadZoneX) camPos.x = desired.x + deadZoneX;

        // Y
        if (!lockVertical)
        {
            if (desired.y > camPos.y + deadZoneY) camPos.y = desired.y - deadZoneY;
            else if (desired.y < camPos.y - deadZoneY) camPos.y = desired.y + deadZoneY;
        }
        else
        {
            // trava o Y no primeiro frame e mantém estável (mais o offset)
            if (!lockedYInitialized)
            {
                lockedBaseY = camPos.y;   // memoriza o Y atual como base
                lockedYInitialized = true;
            }
            camPos.y = lockedBaseY;       // trava o Y
        }

        // aplica o OFFSET vertical fixo (sobe/abaixa a moldura)
        camPos.y += verticalOffset;

        // suavização opcional
        if (smooth > 0f)
            camPos = Vector3.Lerp(transform.position, camPos, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        // clampa nos limites do mundo
        camPos = ClampToWorld(camPos);

        // pixel snap
        if (pixelSnap && ppu > 0)
        {
            float upp = 1f / ppu;
            camPos.x = Mathf.Round(camPos.x / upp) * upp;
            camPos.y = Mathf.Round(camPos.y / upp) * upp;
        }

        transform.position = camPos;
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
        float halfW = halfH * (cam ? cam.aspect : 16f/9f);
        Vector3 c = transform.position;

        float dzW = Mathf.Max(0.01f, (halfW - deadZoneX) * 2f);
        float dzH = lockVertical ? 0.01f : Mathf.Max(0.01f, (halfH - deadZoneY) * 2f);

        Gizmos.DrawWireCube(new Vector3(c.x, c.y, 0f), new Vector3(dzW, dzH, 0.01f));
    }
}
