using UnityEngine;
using UnityEngine.SceneManagement;  // carregar cenas
using UnityEngine.UI;               // botões

public class MainMenuController : MonoBehaviour
{
    [Header("Painéis")]
    [SerializeField] private GameObject menuPanel;   // MenuOptions
    [SerializeField] private GameObject aboutPanel;  // opcional

    [Header("Botões")]
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnAbout;
    [SerializeField] private Button btnQuit;
    [SerializeField] private Button btnBack;         // do About (opcional)

    [Header("Cenas")]
    [SerializeField] private string levelSceneName = "Level1"; // nome exato da cena

    private void Awake()
    {
        // Garante estados iniciais
        if (menuPanel)  menuPanel.SetActive(true);
        if (aboutPanel) aboutPanel.SetActive(false);

        // Liga callbacks
        if (btnStart) btnStart.onClick.AddListener(OnStartClicked);
        if (btnAbout) btnAbout.onClick.AddListener(OnAboutClicked);
        if (btnQuit)  btnQuit.onClick.AddListener(OnQuitClicked);
        if (btnBack)  btnBack.onClick.AddListener(OnBackClicked);
    }

    private void OnDestroy()
    {
        // Desliga callbacks (boa prática)
        if (btnStart) btnStart.onClick.RemoveListener(OnStartClicked);
        if (btnAbout) btnAbout.onClick.RemoveListener(OnAboutClicked);
        if (btnQuit)  btnQuit.onClick.RemoveListener(OnQuitClicked);
        if (btnBack)  btnBack.onClick.RemoveListener(OnBackClicked);
    }

    // === Ações ===
    public void OnStartClicked()
    {
        // Se tiver usado Time.timeScale = 0 no menu, lembre de voltar
        Time.timeScale = 1f;
        SceneManager.LoadScene(levelSceneName);
    }

    public void OnAboutClicked()
    {
        if (!aboutPanel) return;
        menuPanel?.SetActive(false);
        aboutPanel.SetActive(true);
    }

    public void OnBackClicked()
    {
        if (!aboutPanel) return;
        aboutPanel.SetActive(false);
        menuPanel?.SetActive(true);
    }

    public void OnQuitClicked()
    {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }
}
