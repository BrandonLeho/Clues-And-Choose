using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

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

    [Header("Fly Target")]
    [SerializeField] RectTransform flyTargetAnchor;

    static readonly Dictionary<string, ScoreBannerEntry> Registry = new();

    string ownerName;
    public string OwnerName => ownerName;

    public void Initialize(string playerName, int initialScore = 0)
    {
        ownerName = playerName;

        if (nameText) nameText.text = ownerName;
        if (scoreText) scoreText.text = initialScore.ToString();

        RefreshColor();
        SubscribeScore();

        Register();
    }

    void OnEnable() { SubscribeScore(); Register(); }
    void OnDisable() { UnsubscribeScore(); Unregister(); }
    void OnDestroy() { Unregister(); }

    void Register()
    {
        if (string.IsNullOrEmpty(ownerName)) return;
        Registry[ownerName] = this;
    }

    void Unregister()
    {
        if (string.IsNullOrEmpty(ownerName)) return;
        if (Registry.TryGetValue(ownerName, out var cur) && cur == this)
            Registry.Remove(ownerName);
    }

    public static bool TryGetFlyTargetFor(string playerName, out RectTransform target)
    {
        target = null;
        if (string.IsNullOrEmpty(playerName)) return false;
        if (!Registry.TryGetValue(playerName, out var entry) || !entry) return false;

        target = entry.flyTargetAnchor
              ? entry.flyTargetAnchor
              : (entry.scoreText ? entry.scoreText.rectTransform
                                 : entry.GetComponent<RectTransform>());

        return target;
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
