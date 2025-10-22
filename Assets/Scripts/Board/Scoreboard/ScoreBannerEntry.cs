using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class ScoreBannerEntry : MonoBehaviour
{
    public static readonly Dictionary<string, RectTransform> NameToScoreAnchor = new();

    [Header("Refs")]
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] Image bannerBG;

    [Header("Visuals")]
    [SerializeField] Color fallbackBG = Color.white;

    [Header("Glow")]
    [SerializeField] private BannerGlowController glowBinder;

    string ownerName;

    public RectTransform ScoreAnchor => scoreText ? scoreText.rectTransform : (RectTransform)transform;

    public void Initialize(string playerName, int initialScore = 0)
    {
        ownerName = playerName;

        if (nameText) nameText.text = ownerName;
        if (scoreText) scoreText.text = initialScore.ToString();

        RefreshColor();
        SubscribeScore();
        RegisterSelf();
    }

    void OnEnable()
    {
        SubscribeScore();
        RegisterSelf();
    }

    void OnDisable()
    {
        UnsubscribeScore();
        if (!string.IsNullOrEmpty(ownerName))
            NameToScoreAnchor.Remove(ownerName);
    }

    void RegisterSelf()
    {
        if (!string.IsNullOrEmpty(ownerName))
            NameToScoreAnchor[ownerName] = ScoreAnchor;
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
}
