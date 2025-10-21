using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class ListPlayer : MonoBehaviour
{
    public enum RepeatMode { None, RepeatAll, RepeatOne }

    [Header("Playlist")]
    [SerializeField] private AudioPlaylist playlist;
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private bool shuffle = false;
    [SerializeField] private RepeatMode repeat = RepeatMode.RepeatAll;

    [Header("Volume / Mix")]
    [Range(0f, 1f)] public float volume = 0.5f;
    [Min(0.05f)] public float crossfadeTime = 1.2f;

    [Header("Eventos")]
    public UnityEvent<AudioClip> onTrackChanged;
    public UnityEvent onPlaylistEnded;

    // Two AudioSources para crossfade
    private AudioSource _a, _b;
    private int _index = -1;
    private bool _isFading;
    private bool _started;

    private void Awake()
    {
        _a = gameObject.AddComponent<AudioSource>();
        _b = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { _a, _b })
        {
            s.playOnAwake = false;
            s.loop = false;        // playlist avança sozinha
            s.spatialBlend = 0f;   // 2D
            s.volume = 0f;
        }
    }

    private void Start()
    {
        if (autoPlayOnStart)
            PlayFromStart();
    }

    private void Update()
    {
        if (!_started || _isFading) return;

        // Quando a trilha atual acabar, avança
        bool aEnded = !_a.isPlaying && _a.time > 0f;
        bool bEnded = !_b.isPlaying && _b.time > 0f;

        if (aEnded || bEnded)
            PlayNext();
    }

    // === API pública ===

    public void PlayFromStart()
    {
        if (!HasTracks()) return;
        _started = true;
        _index = shuffle ? Random.Range(0, playlist.tracks.Length) : 0;
        StartCoroutine(CrossfadeTo(playlist.tracks[_index]));
    }

    public void PlayIndex(int index)
    {
        if (!HasTracks()) return;
        index = Mathf.Clamp(index, 0, playlist.tracks.Length - 1);
        _index = index;
        _started = true;
        StartCoroutine(CrossfadeTo(playlist.tracks[_index]));
    }

    public void PlayNext()
    {
        if (!HasTracks()) return;

        if (repeat == RepeatMode.RepeatOne)
        {
            StartCoroutine(CrossfadeTo(playlist.tracks[_index]));
            return;
        }

        if (shuffle)
        {
            _index = Random.Range(0, playlist.tracks.Length);
        }
        else
        {
            _index++;
            if (_index >= playlist.tracks.Length)
            {
                if (repeat == RepeatMode.RepeatAll)
                    _index = 0;
                else
                {
                    _started = false;
                    onPlaylistEnded?.Invoke();
                    return;
                }
            }
        }

        StartCoroutine(CrossfadeTo(playlist.tracks[_index]));
    }

    public void PlayPrevious()
    {
        if (!HasTracks()) return;

        if (repeat == RepeatMode.RepeatOne)
        {
            StartCoroutine(CrossfadeTo(playlist.tracks[_index]));
            return;
        }

        if (shuffle)
        {
            _index = Random.Range(0, playlist.tracks.Length);
        }
        else
        {
            _index--;
            if (_index < 0)
            {
                if (repeat == RepeatMode.RepeatAll)
                    _index = playlist.tracks.Length - 1;
                else
                {
                    _index = 0;
                    return;
                }
            }
        }

        StartCoroutine(CrossfadeTo(playlist.tracks[_index]));
    }

    public void Pause()
    {
        if (_a.isPlaying) _a.Pause();
        if (_b.isPlaying) _b.Pause();
    }

    public void Resume()
    {
        if (_a.clip && !_a.isPlaying) _a.Play();
        if (_b.clip && !_b.isPlaying) _b.Play();
    }

    public void Stop(float fadeOut = 0.5f)
    {
        StartCoroutine(FadeOutAll(fadeOut));
        _started = false;
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (_a.isPlaying) _a.volume = Mathf.Min(_a.volume, volume);
        if (_b.isPlaying) _b.volume = Mathf.Min(_b.volume, volume);
    }

    public AudioClip CurrentClip =>
        _a.isPlaying ? _a.clip : _b.isPlaying ? _b.clip : null;

    // === Internals ===

    private bool HasTracks()
    {
        return playlist != null && playlist.tracks != null && playlist.tracks.Length > 0;
    }

    private IEnumerator CrossfadeTo(AudioClip next)
    {
        if (next == null) yield break;

        _isFading = true;

        // Decide quem é FROM (tocando) e TO (entrando)
        AudioSource from = _a.isPlaying ? _a : _b;
        AudioSource to   = _a.isPlaying ? _b : _a;

        // Prepara o TO
        to.clip = next;
        to.volume = 0f;
        to.loop = false;
        to.Play();

        onTrackChanged?.Invoke(next);

        float t = 0f;
        float dur = Mathf.Max(0.05f, crossfadeTime);
        float startFrom = from.volume;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // não depende do timeScale
            float k = t / dur;
            to.volume   = Mathf.Lerp(0f, volume, k);
            from.volume = Mathf.Lerp(startFrom, 0f, k);
            yield return null;
        }

        to.volume = volume;
        from.Stop();
        from.volume = 0f;

        _isFading = false;
    }

    private IEnumerator FadeOutAll(float dur)
    {
        float a0 = _a.volume;
        float b0 = _b.volume;
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - (t / dur);
            _a.volume = a0 * k;
            _b.volume = b0 * k;
            yield return null;
        }

        _a.Stop(); _b.Stop();
        _a.volume = _b.volume = 0f;
    }
}
