using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class MenuMusicPlayer : MonoBehaviour
{
    [Header("Clip (importado como Streaming)")]
    [SerializeField] private AudioClip musicClip;

    [Header("Volumes e Fade")]
    [Range(0f, 1f)] public float targetVolume = 0.6f;
    public float fadeInSeconds = 1.5f;
    public float fadeOutSeconds = 0.8f;

    private AudioSource _src;

    private void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.clip = musicClip;
        _src.loop = true;
        _src.spatialBlend = 0f; // 2D
        _src.playOnAwake = false; // vamos controlar via script
        _src.volume = 0f;
    }

    private void OnEnable()
    {
        if (musicClip != null)
            StartCoroutine(FadeInPlay());
    }

    public IEnumerator FadeInPlay()
    {
        if (!_src.isPlaying) _src.Play();
        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            _src.volume = Mathf.Lerp(0f, targetVolume, t / Mathf.Max(0.01f, fadeInSeconds));
            yield return null;
        }
        _src.volume = targetVolume;
    }

    public IEnumerator FadeOutStop()
    {
        float start = _src.volume;
        float t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            _src.volume = Mathf.Lerp(start, 0f, t / Mathf.Max(0.01f, fadeOutSeconds));
            yield return null;
        }
        _src.volume = 0f;
        _src.Stop();
    }
}
