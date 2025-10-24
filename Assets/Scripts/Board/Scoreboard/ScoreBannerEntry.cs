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

    [Header("Score Count Animation")]
    [SerializeField, Min(0.05f)] float countDuration = 0.45f;
    [SerializeField] AnimationCurve countEase = AnimationCurve.Linear(0, 0, 1, 1);

    int _displayedScore;
    int _authoritativeScore;
    System.Collections.Generic.Queue<int> _pendingDeltas = new();
    Coroutine _countCo;

    static readonly Dictionary<string, ScoreBannerEntry> Registry = new();

    string ownerName;
    public string OwnerName => ownerName;

    public void Initialize(string playerName, int initialScore = 0)
    {
        ownerName = playerName;

        if (nameText) nameText.text = ownerName;
        if (scoreText) scoreText.text = initialScore.ToString();

        var current = ScoreRegistry.GetScore(ownerName);
        _displayedScore = _authoritativeScore = current;
        if (scoreText) scoreText.text = current.ToString();

        RefreshColor();
        SubscribeScore();

        Register();
    }

    void OnEnable() { SubscribeScore(); Register(); ScorePop.OnScoreFlyArrived += HandleFlyArrived; }
    void OnDisable() { UnsubscribeScore(); Unregister(); ScorePop.OnScoreFlyArrived -= HandleFlyArrived; }
    void OnDestroy() { ScorePop.OnScoreFlyArrived -= HandleFlyArrived; Unregister(); }

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
        if (string.IsNullOrEmpty(ownerName) || name != ownerName) return;

        _authoritativeScore = newScore;

        if (_countCo == null && _pendingDeltas.Count == 0 && scoreText)
        {
            _displayedScore = newScore;
            scoreText.text = newScore.ToString();
        }
    }

    void HandleFlyArrived(string name, int delta)
    {
        if (string.IsNullOrEmpty(ownerName) || name != ownerName) return;
        if (delta <= 0) return;

        _pendingDeltas.Enqueue(delta);
        if (_countCo == null) _countCo = StartCoroutine(CoProcessQueue());
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

    System.Collections.IEnumerator CoProcessQueue()
    {
        while (_pendingDeltas.Count > 0)
        {
            int delta = _pendingDeltas.Dequeue();
            int from = _displayedScore;
            int to = from + delta;

            yield return CoCountTo(from, to, countDuration);
            _displayedScore = to;

            if (_displayedScore < _authoritativeScore)
            {
                int catchTo = _authoritativeScore;
                float quick = Mathf.Min(countDuration * 0.5f, 0.35f);
                yield return CoCountTo(_displayedScore, catchTo, quick);
                _displayedScore = catchTo;
            }
        }
        _countCo = null;
    }

    System.Collections.IEnumerator CoCountTo(int from, int to, float dur)
    {
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        int last = from;

        while (t < dur)
        {
            t += UnityEngine.Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = countEase != null ? countEase.Evaluate(u) : u;

            int val = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Lerp(from, to + 0.999f, e)),
                from, to
            );

            if (val != last)
            {
                last = val;
                if (scoreText) scoreText.text = val.ToString();
            }
            yield return null;
        }

        if (scoreText) scoreText.text = to.ToString();
    }
}
