using UnityEngine;

/// <summary>
/// Simple helper that copies a sprite, floats upward and fades out.
/// </summary>
public class GhostRiseEffect : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    Color startColor;
    float lifetime;
    float riseSpeed;
    float elapsed;

    public static GhostRiseEffect SpawnFrom(SpriteRenderer source, Color color, float lifetime, float riseSpeed, int sortingOffset = 1)
    {
        if (!source || !source.sprite || lifetime <= 0f) return null;

        GameObject go = new GameObject("PlayerGhost");
        go.transform.position = source.transform.position;
        go.transform.localScale = source.transform.lossyScale;

        var effect = go.AddComponent<GhostRiseEffect>();
        effect.Initialize(source, color, lifetime, riseSpeed, sortingOffset);
        return effect;
    }

    void Initialize(SpriteRenderer source, Color color, float duration, float speed, int sortingOffset)
    {
        lifetime = Mathf.Max(0.01f, duration);
        riseSpeed = speed;
        startColor = color;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = source.sprite;
        spriteRenderer.flipX = source.flipX;
        spriteRenderer.flipY = source.flipY;
        spriteRenderer.sortingLayerID = source.sortingLayerID;
        spriteRenderer.sortingOrder = source.sortingOrder + sortingOffset;
        spriteRenderer.material = source.sharedMaterial;
        spriteRenderer.color = startColor;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        if (spriteRenderer)
        {
            float t = Mathf.Clamp01(elapsed / lifetime);
            float alpha = Mathf.Lerp(startColor.a, 0f, t);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
        }

        if (elapsed >= lifetime)
            Destroy(gameObject);
    }
}
