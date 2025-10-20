using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ScoreBannerEntry : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] Image bannerBG;

    [Header("Visuals")]
    [SerializeField] Color fallbackBG = Color.white;

    [Header("Glow")]
    [SerializeField] private BannerGlowController glowBinder;

    string ownerName;

    public void Initialize(string playerName, int initialScore = 0)
    {
        ownerName = playerName;

        if (nameText) nameText.text = ownerName;
        if (scoreText) scoreText.text = initialScore.ToString();

        if (!string.IsNullOrWhiteSpace(ownerName) && scoreText)
        {
            var rt = scoreText.rectTransform;
            _scoreAnchors[ownerName] = rt;
        }

        RefreshColor();
        SubscribeScore();
    }

    void OnEnable() => SubscribeScore();
    void OnDisable()
    {
        if (!string.IsNullOrWhiteSpace(ownerName))
            _scoreAnchors.Remove(ownerName);

        UnsubscribeScore();
    }

    void SubscribeScore()
    {
        UnsubscribeScore();
        ScoreRegistry.OnScoreChanged += HandleScoreChanged;
    }

    void UnsubscribeScore()
    {
        ScoreRegistry.OnScoreChanged -= HandleScoreChanged;
    }

    void HandleScoreChanged(string name, int newScore)
    {
        if (!string.IsNullOrEmpty(ownerName) && name == ownerName && scoreText)
            scoreText.text = newScore.ToString();
    }

    public void RefreshColor()
    {
        if (bannerBG == null) return;

        Color c;
        if (RegistryNameColorLookup.TryGetColorForName(ownerName, out c))
        {
            var invisible = bannerBG.color; invisible.a = 0f;
            bannerBG.color = invisible;

            if (glowBinder) glowBinder.SetPlayerGlowColor(c);
        }
        else
        {
            var invisible = fallbackBG; invisible.a = 0f;
            bannerBG.color = invisible;

            if (glowBinder) glowBinder.SetPlayerGlowColor(fallbackBG);
        }
    }

    static readonly System.Collections.Generic.Dictionary<string, RectTransform> _scoreAnchors = new System.Collections.Generic.Dictionary<string, RectTransform>();

    public static RectTransform TryGetScoreAnchor(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        _scoreAnchors.TryGetValue(name, out var r);
        return r;
    }

}
