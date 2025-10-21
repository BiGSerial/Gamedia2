using TMPro;
using UnityEngine;

/// <summary>
/// Atualiza textos da HUD com as estatísticas do GameController.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text livesText;
    [SerializeField] TMP_Text fruitText;
    [SerializeField] TMP_Text hiScoreText;

    [Header("Labels")]
    [SerializeField] string scoreLabel = "Score: ";
    [SerializeField] string livesLabel = "Lives: ";
    [SerializeField] string fruitsLabel = "Fruits: ";
    [SerializeField] string hiScoreLabel = "Hi-Score: ";

    void OnEnable()
    {
        TrySubscribe();
        UpdateTexts();
    }

    void Start()
    {
        TrySubscribe();
        UpdateTexts();
    }

    void OnDisable()
    {
        if (GameController.Instance != null)
            GameController.Instance.StatsChanged -= HandleStatsChanged;
    }

    void TrySubscribe()
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.StatsChanged -= HandleStatsChanged;
            GameController.Instance.StatsChanged += HandleStatsChanged;
        }
    }

    void HandleStatsChanged() => UpdateTexts();

    void UpdateTexts()
    {
        var gc = GameController.Instance;
        if (!gc)
        {
            ApplyText(scoreText, scoreLabel + "0");
            ApplyText(livesText, livesLabel + "0");
            ApplyText(fruitText, fruitsLabel + "0");
            ApplyText(hiScoreText, hiScoreLabel + "0");
            return;
        }

        ApplyText(scoreText, scoreLabel + gc.Score.ToString("000000"));
        ApplyText(livesText, livesLabel + gc.Lives.ToString());

        if (gc.FruitsPerExtraLife > 0)
        {
            int progress = gc.FruitsTowardsExtraLife;
            ApplyText(fruitText, fruitsLabel + $"{gc.FruitsCollected} ({progress}/{gc.FruitsPerExtraLife})");
        }
        else
        {
            ApplyText(fruitText, fruitsLabel + gc.FruitsCollected.ToString());
        }

        ApplyText(hiScoreText, hiScoreLabel + gc.HighScore.ToString("000000"));
    }

    static void ApplyText(TMP_Text text, string value)
    {
        if (text) text.text = value;
    }
}
