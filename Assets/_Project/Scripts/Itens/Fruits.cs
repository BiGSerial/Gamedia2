using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Fruits : MonoBehaviour
{
    [Header("Valores")]
    [Min(0)] public int scoreValue = 10;     // pontos
    [Min(0)] public int collectAmount = 1;   // qtd de "frutas" para o contador

    [Header("Coleta")]
    [SerializeField] string playerTag = "Player";
    public bool destroyOnCollect = true;

    [Header("Feedback (opcional)")]
    public GameObject pickupEffectPrefab;    // efeito que aparece APÓS coletar
    public float effectLifetime = 1.5f;      // tempo para destruir o efeito
    public AudioClip pickupSfx;
    [Range(0f,1f)] public float sfxVolume = 0.9f;

    [Header("Eventos (plugue seu GameController no futuro)")]
    // Envia (scoreValue, collectAmount)
    public UnityEvent<int,int> onCollected;
    public UnityEvent onCollectedSimple;

    bool collected;

    void Reset()
    {
        // garantir que o Collider2D esteja como trigger
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (!other.CompareTag(playerTag)) return;

        Collect();
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;

        // 1) Efeito visual
        if (pickupEffectPrefab)
        {
            var fx = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            if (effectLifetime > 0f) Destroy(fx, effectLifetime);
        }

        // 2) Som
        if (pickupSfx)
            AudioSource.PlayClipAtPoint(pickupSfx, Camera.main ? Camera.main.transform.position : transform.position, sfxVolume);

        // 3) Eventos (conecte seu GameController aqui depois)
        onCollected?.Invoke(scoreValue, collectAmount);
        onCollectedSimple?.Invoke();

        // 4) Some com a fruta
        if (destroyOnCollect) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
