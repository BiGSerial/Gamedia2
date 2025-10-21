using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonSFX : MonoBehaviour,
    IPointerEnterHandler, IPointerClickHandler, ISelectHandler, ISubmitHandler
{
    [Header("Clipes")]
    [SerializeField] private AudioClip hoverClip;  // foco/hover
    [SerializeField] private AudioClip clickClip;  // clique/submit

    [Header("Áudio de saída (no Canvas)")]
    [SerializeField] private AudioSource uiAudio;  // coloque um só no Canvas

    [Header("Opções")]
    public bool playOnPointerEnter = true; // mouse hover
    public bool playOnSelect = true;       // foco por teclado/controle
    public bool playOnClick = true;        // mouse
    public bool playOnSubmit = true;       // botão “Submit” (A/Enter)

    private Button _btn;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        if (!uiAudio)
            uiAudio = GetComponentInParent<AudioSource>(); // fallback
        if (uiAudio) uiAudio.spatialBlend = 0f; // 2D
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playOnPointerEnter && hoverClip && IsInteractable())
            uiAudio?.PlayOneShot(hoverClip);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (playOnSelect && hoverClip && IsInteractable())
            uiAudio?.PlayOneShot(hoverClip);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playOnClick && clickClip && IsInteractable())
            uiAudio?.PlayOneShot(clickClip);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (playOnSubmit && clickClip && IsInteractable())
            uiAudio?.PlayOneShot(clickClip);
    }

    private bool IsInteractable() => _btn == null || _btn.interactable;
}
