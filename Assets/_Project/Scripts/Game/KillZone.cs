using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
public class KillZone : MonoBehaviour
{
    [Tooltip("Arraste um Transform (checkpoint). Se vazio, recarrega a cena.")]
    public Transform respawnPoint;

    [Tooltip("Tag do objeto considerado 'player'.")]
    public string playerTag = "Player";

    private void Reset()
    {
        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true; // KillZone é trigger
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (respawnPoint != null)
        {
            // zera velocidade se tiver Rigidbody2D
            var rb = other.attachedRigidbody;
            if (rb) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
            other.transform.position = respawnPoint.position;
        }
        else
        {
            // fallback: reinicia a cena
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (!col) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider2D b) Gizmos.DrawCube(b.offset, b.size);
    }
#endif
}
