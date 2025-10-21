using UnityEngine;

[CreateAssetMenu(fileName = "NewAudioPlaylist", menuName = "Audio/Playlist")]
public class AudioPlaylist : ScriptableObject
{
    [Tooltip("Arraste aqui os clipes (configure cada um como Load Type = Streaming)")]
    public AudioClip[] tracks;
}
