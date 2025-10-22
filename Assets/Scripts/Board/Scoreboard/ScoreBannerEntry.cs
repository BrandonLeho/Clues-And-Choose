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

    [Header("FX Target")]
    [Tooltip("Where score texts should fly to (defaults to this RectTransform center).")]
    [SerializeField] RectTransform flyTargetAnchor;

    string ownerName;
    public string OwnerName => ownerName;
    public RectTransform FlyTargetAnchor => flyTargetAnchor ? flyTargetAnchor : (transform as RectTransform);

    public void Initialize(string playerName, int initialScore = 0)
    {
        ownerName = playerName;
        if (nameText) nameText.text = ownerName;
        if (scoreText) scoreText.text = initialScore.ToString();
        RefreshColor();
        SubscribeScore();
    }

    void OnEnable() => SubscribeScore();
    void OnDisable() => UnsubscribeScore();

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

    public void PulseGlow()
    {
        if (!glowBinder) return;
        glowBinder.outlineIntensity = Mathf.Min(glowBinder.outlineIntensity + 0.6f, 5f);
        glowBinder.topIntensity = Mathf.Min(glowBinder.topIntensity + 0.6f, 5f);
    }
}
