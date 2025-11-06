using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using Mirror;

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

    [Header("Glow On Count")]
    [SerializeField] bool glowOnCount = true;
    [SerializeField, Min(0f)] float glowRiseTime = 0.18f;
    [SerializeField, Min(0f)] float glowHoldTime = 0.20f;
    [SerializeField, Min(0f)] float glowSettleTime = 0.30f;
    [SerializeField] AnimationCurve glowInEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] AnimationCurve glowOutEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] bool useUnscaledTime = true;
    float DT => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    [Header("Peak Glow Targets")]
    [Range(0f, 1f)] public float glowTopHeight = 0.9f;
    [Range(0f, 1f)] public float glowTopFeather = 0.15f;
    [Range(0f, 5f)] public float glowTopIntensity = 3.0f;

    [Range(0f, 0.5f)] public float glowOutlineThickness = 0.25f;
    [Range(0f, 1f)] public float glowOutlineFeather = 0.25f;
    [Range(1f, 8f)] public float glowOutlineTopFalloff = 2.5f;
    [Range(0f, 5f)] public float glowOutlineIntensity = 3.0f;

    int _displayedScore;
    int _authoritativeScore;
    Queue<int> _pendingDeltas = new();
    Coroutine _countCo;
    bool _glowArmed;

    Coroutine _glowCo;

    static readonly Dictionary<string, ScoreBannerEntry> Registry = new();

    string ownerName;
    public string OwnerName => ownerName;

    BannerGlowController _glow;
    float _baseTopHeight, _baseTopFeather, _baseTopIntensity;
    float _baseOutThick, _baseOutFeather, _baseOutTopFalloff, _baseOutIntensity;

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

    void Awake()
    {
        _glow = glowBinder;
        CaptureGlowBaseline();
    }

    void OnEnable() { SubscribeScore(); Register(); ScorePop.OnScoreFlyArrived += HandleFlyArrived; }

    void OnDisable()
    {
        UnsubscribeScore();
        Unregister();
        ScorePop.OnScoreFlyArrived -= HandleFlyArrived;

        if (_glowCo != null)
        {
            StopCoroutine(_glowCo);
            _glowCo = null;
        }

        _glowArmed = false;

        RestoreGlowBaseline();
    }

    void OnDestroy()
    {
        ScorePop.OnScoreFlyArrived -= HandleFlyArrived;
        Unregister();

        RestoreGlowBaseline();
    }

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

        if (_pendingDeltas.Count == 0)
        {
            if (_countCo != null) StopCoroutine(_countCo);

            if (_displayedScore != _authoritativeScore)
                _countCo = StartCoroutine(CoAnimateToAuthoritative(_authoritativeScore));
            else
                _countCo = null;
        }
    }

    void HandleFlyArrived(string name, int delta)
    {
        if (string.IsNullOrEmpty(ownerName) || name != ownerName) return;
        if (delta <= 0) return;

        if (!NetworkServer.active) return;

        _pendingDeltas.Enqueue(delta);

        if (glowOnCount && !_glowArmed)
        {
            _glowArmed = true;
            TriggerGlowPulse();
        }

        if (_countCo == null)
            _countCo = StartCoroutine(CoProcessQueue());
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

    IEnumerator CoProcessQueue()
    {
        if (glowOnCount && !_glowArmed)
        {
            _glowArmed = true;
            TriggerGlowPulse();
        }

        while (_pendingDeltas.Count > 0)
        {
            int delta = _pendingDeltas.Dequeue();
            int from = _displayedScore;
            int to = from + delta;

            yield return CoCountTo(from, to, countDuration);
            _displayedScore = to;

            if (_displayedScore != _authoritativeScore)
            {
                int catchFrom = _displayedScore;
                int catchTo = _authoritativeScore;
                float quick = Mathf.Min(countDuration * 0.5f, 0.35f);
                yield return CoCountTo(catchFrom, catchTo, quick);
                _displayedScore = catchTo;
            }
        }

        if (_displayedScore != _authoritativeScore)
        {
            float quick = Mathf.Min(countDuration * 0.5f, 0.35f);
            yield return CoCountTo(_displayedScore, _authoritativeScore, quick);
            _displayedScore = _authoritativeScore;
        }

        _glowArmed = false;
        _countCo = null;
    }

    IEnumerator CoCountTo(int from, int to, float dur)
    {
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        int last = from;

        int lo = Mathf.Min(from, to);
        int hi = Mathf.Max(from, to);
        bool up = to >= from;

        while (t < dur)
        {
            t += DT;
            float u = Mathf.Clamp01(t / dur);
            float e = countEase != null ? countEase.Evaluate(u) : u;

            int val = up
                ? Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(from, to + 0.999f, e)), lo, hi)
                : Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(from, to - 0.999f, e)), lo, hi);

            if (val != last)
            {
                last = val;
                if (scoreText) scoreText.text = val.ToString();
            }
            yield return null;
        }

        if (scoreText) scoreText.text = to.ToString();
    }

    IEnumerator CoAnimateToAuthoritative(int target)
    {
        int from = _displayedScore;
        float dur = countDuration;
        yield return CoCountTo(from, target, dur);
        _displayedScore = target;

        if (_displayedScore != _authoritativeScore)
        {
            float quick = Mathf.Min(countDuration * 0.5f, 0.35f);
            yield return CoCountTo(_displayedScore, _authoritativeScore, quick);
            _displayedScore = _authoritativeScore;
        }

        if (_pendingDeltas.Count > 0)
        {
            _countCo = StartCoroutine(CoProcessQueue());
        }
        else
        {
            _glowArmed = false;
            _countCo = null;
        }
    }

    void TriggerGlowPulse()
    {
        if (!glowBinder) return;
        if (_glowCo != null) StopCoroutine(_glowCo);
        _glowCo = StartCoroutine(CoGlowPulse());
    }

    IEnumerator CoGlowPulse()
    {
        var g = glowBinder;
        if (!g) yield break;

        float bTopHeight = _glow ? _baseTopHeight : g.topHeight;
        float bTopFeather = _glow ? _baseTopFeather : g.topFeather;
        float bTopIntensity = _glow ? _baseTopIntensity : g.topIntensity;

        float bOutThick = _glow ? _baseOutThick : g.outlineThickness;
        float bOutFeather = _glow ? _baseOutFeather : g.outlineFeather;
        float bOutTopFalloff = _glow ? _baseOutTopFalloff : g.outlineTopFalloff;
        float bOutIntensity = _glow ? _baseOutIntensity : g.outlineIntensity;

        float t = 0f, d = Mathf.Max(0.0001f, glowRiseTime);
        while (t < d)
        {
            t += DT;
            float u = Mathf.Clamp01(t / d);
            float e = glowInEase != null ? glowInEase.Evaluate(u) : u;

            glowBinder.topHeight = Mathf.Lerp(bTopHeight, glowTopHeight, e);
            glowBinder.topFeather = Mathf.Lerp(bTopFeather, glowTopFeather, e);
            glowBinder.topIntensity = Mathf.Lerp(bTopIntensity, glowTopIntensity, e);
            glowBinder.outlineThickness = Mathf.Lerp(bOutThick, glowOutlineThickness, e);
            glowBinder.outlineFeather = Mathf.Lerp(bOutFeather, glowOutlineFeather, e);
            glowBinder.outlineTopFalloff = Mathf.Lerp(bOutTopFalloff, glowOutlineTopFalloff, e);
            glowBinder.outlineIntensity = Mathf.Lerp(bOutIntensity, glowOutlineIntensity, e);

            glowBinder.ApplyAll();
            yield return null;
        }

        float hold = Mathf.Max(0f, glowHoldTime);
        if (hold > 0f) { float h = 0f; while (h < hold) { h += DT; yield return null; } } // was Time.deltaTime

        t = 0f; d = Mathf.Max(0.0001f, glowSettleTime);
        while (t < d)
        {
            t += DT;
            float u = Mathf.Clamp01(t / d);
            float e = glowOutEase != null ? glowOutEase.Evaluate(u) : u;

            glowBinder.topHeight = Mathf.Lerp(glowTopHeight, bTopHeight, e);
            glowBinder.topFeather = Mathf.Lerp(glowTopFeather, bTopFeather, e);
            glowBinder.topIntensity = Mathf.Lerp(glowTopIntensity, bTopIntensity, e);
            glowBinder.outlineThickness = Mathf.Lerp(glowOutlineThickness, bOutThick, e);
            glowBinder.outlineFeather = Mathf.Lerp(glowOutlineFeather, bOutFeather, e);
            glowBinder.outlineTopFalloff = Mathf.Lerp(glowOutlineTopFalloff, bOutTopFalloff, e);
            glowBinder.outlineIntensity = Mathf.Lerp(glowOutlineIntensity, bOutIntensity, e);

            glowBinder.ApplyAll();
            yield return null;
        }

        glowBinder.topHeight = bTopHeight;
        glowBinder.topFeather = bTopFeather;
        glowBinder.topIntensity = bTopIntensity;
        glowBinder.outlineThickness = bOutThick;
        glowBinder.outlineFeather = bOutFeather;
        glowBinder.outlineTopFalloff = bOutTopFalloff;
        glowBinder.outlineIntensity = bOutIntensity;

        glowBinder.ApplyAll();
        _glowCo = null;
    }

    void CaptureGlowBaseline()
    {
        if (!_glow) return;
        _baseTopHeight = _glow.topHeight;
        _baseTopFeather = _glow.topFeather;
        _baseTopIntensity = _glow.topIntensity;
        _baseOutThick = _glow.outlineThickness;
        _baseOutFeather = _glow.outlineFeather;
        _baseOutTopFalloff = _glow.outlineTopFalloff;
        _baseOutIntensity = _glow.outlineIntensity;
    }

    void RestoreGlowBaseline()
    {
        if (!_glow) return;
        _glow.topHeight = _baseTopHeight;
        _glow.topFeather = _baseTopFeather;
        _glow.topIntensity = _baseTopIntensity;
        _glow.outlineThickness = _baseOutThick;
        _glow.outlineFeather = _baseOutFeather;
        _glow.outlineTopFalloff = _baseOutTopFalloff;
        _glow.outlineIntensity = _baseOutIntensity;
        _glow.ApplyAll();
    }
}
