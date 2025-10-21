using UnityEngine;
using System.Collections;

public class PlayerRespawnOnOffscreen : MonoBehaviour
{
    [Header("Refs")]
    public PlayerDeath playerDeath;              // arraste o Player (com PlayerDeath)
    public SideScrollCamera2D sideScrollCam;     // arraste a Main Camera com esse script
    public Transform checkpoint;                 // ponto de respawn

    [Header("Detecção offscreen")]
    [Range(0f, 0.2f)]
    public float viewportMargin = 0.05f;         // margem extra em viewport
    public int requiredOffscreenFrames = 6;      // frames consecutivos fora
    [Min(0f)]
    public float respawnDelay = 5f;              // atraso após sair da tela

    Coroutine waitRoutine;
    Camera _cam; // câmera real usada pelo sideScrollCam

    void Awake() => EnsureReferences();

    void OnEnable()
    {
        EnsureReferences();
        if (playerDeath != null) playerDeath.OnDied += HandlePlayerDied;
    }

    void OnDisable()
    {
        if (playerDeath != null) playerDeath.OnDied -= HandlePlayerDied;
    }

    public void HandlePlayerDied(PlayerDeath _ignored = null)
    {
        EnsureReferences();
        if (GameController.Instance)
            GameController.Instance.LockCameraForDeath();
        else if (sideScrollCam)
            sideScrollCam.SetLockVertical(true, true);

        if (waitRoutine != null) StopCoroutine(waitRoutine);
        waitRoutine = StartCoroutine(WaitOffscreenThenRespawn());
    }

    IEnumerator WaitOffscreenThenRespawn()
    {
        if (!_cam || !playerDeath) yield break;

        int offFrames = 0;
        bool ghostSpawned = false;

        while (true)
        {
            if (!playerDeath) yield break;

            Vector3 worldPos = playerDeath.transform.position;
            Vector3 vp = _cam.WorldToViewportPoint(worldPos);

            bool offscreen;
            if (vp.z < 0f)
            {
                offscreen = true;
            }
            else
            {
                float min = 0f - viewportMargin;
                float max = 1f + viewportMargin;
                bool inside = vp.x >= min && vp.x <= max && vp.y >= min && vp.y <= max;
                offscreen = !inside;
            }

            if (offscreen)
            {
                offFrames++;

                if (!ghostSpawned && vp.z >= 0f && vp.y < 0f)
                {
                    float depth = worldPos.z - _cam.transform.position.z;
                    Vector3 spawnPos = _cam.ViewportToWorldPoint(new Vector3(Mathf.Clamp01(vp.x), 0f, depth));
                    playerDeath.SpawnGhostVisual(spawnPos);
                    ghostSpawned = true;
                }
            }
            else
            {
                offFrames = 0;
            }

            if (offFrames >= Mathf.Max(1, requiredOffscreenFrames))
                break;

            yield return null;
        }

        if (!ghostSpawned && playerDeath)
        {
            playerDeath.SpawnGhostVisual();
            ghostSpawned = true;
        }

        if (GameController.Instance)
        {
            if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);
            GameController.Instance.RequestRespawn(playerDeath, checkpoint);
        }
        else
        {
            if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);
            if (playerDeath && checkpoint) playerDeath.RespawnAt(checkpoint);
            if (sideScrollCam) sideScrollCam.SetLockVertical(false);
        }

        waitRoutine = null;
    }

    void EnsureReferences()
    {
        if (!playerDeath)
        {
            playerDeath = GetComponent<PlayerDeath>() ?? GetComponentInParent<PlayerDeath>();
        }

        if (!sideScrollCam)
        {
            Camera mainCam = Camera.main;
            if (mainCam) sideScrollCam = mainCam.GetComponent<SideScrollCamera2D>();
            if (!sideScrollCam) sideScrollCam = FindObjectOfType<SideScrollCamera2D>();
        }

        if (!_cam && sideScrollCam) _cam = sideScrollCam.GetComponent<Camera>();
    }
}
