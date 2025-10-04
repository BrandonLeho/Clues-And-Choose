using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardBanner : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Image bg;

    [Header("Text Settings")]
    [SerializeField] private TextOverflowModes overflowMode = TextOverflowModes.Overflow;

    public int CurrentScore { get; private set; }

    void Reset()
    {
        if (!nameText) nameText = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
        if (!scoreText) scoreText = transform.Find("Score")?.GetComponent<TextMeshProUGUI>();
        if (!bg) bg = transform.Find("BG")?.GetComponent<Image>();
    }

    public void SetName(string displayName, float maxNameFontSize = 0f)
    {
        if (!nameText) return;
        nameText.text = displayName;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = overflowMode;

        if (maxNameFontSize > 0f)
        {
            nameText.enableAutoSizing = true;
            nameText.fontSizeMax = maxNameFontSize;
            nameText.fontSizeMin = Mathf.Max(10f, maxNameFontSize * 0.35f);
        }
    }

    public void SetScore(int score)
    {
        CurrentScore = score;
        if (scoreText)
        {
            scoreText.text = score.ToString();
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    public void SetBackgroundColor(Color c)
    {
        if (bg) bg.color = c;
    }
}
