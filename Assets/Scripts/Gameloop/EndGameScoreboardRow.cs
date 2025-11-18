using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndGameScoreboardRow : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI scoreLabel;
    [SerializeField] RectTransform barFill;
    [SerializeField] Image barImage;

    public void Bind(string playerName, int score, Color color, float barWidth)
    {
        if (nameLabel) nameLabel.text = playerName;
        if (scoreLabel) scoreLabel.text = score.ToString();

        if (barFill)
        {
            var size = barFill.sizeDelta;
            size.x = barWidth;
            barFill.sizeDelta = size;
        }

        if (barImage) barImage.color = color;
    }
}
