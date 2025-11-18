using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TurnNotification : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform root;
    [SerializeField] TMP_Text label;
    [SerializeField] CanvasGroup cg;

    [Header("Children")]
    [SerializeField] RectTransform backgroundRect;
    [SerializeField] RectTransform textRect;

    [Header("Stagger")]
    [SerializeField, Min(0f)] float entryLagPixels = 140f;
    [SerializeField, Min(0f)] float exitLeadPixels = 0f;
    [SerializeField]
    AnimationCurve entryLagDecay = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f)
    );
    [SerializeField]
    AnimationCurve exitLeadGrowth = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Header("Motion")]
    [SerializeField] float offscreenPadding = 200f;
    [SerializeField, Min(0.05f)] float enterDuration = 0.65f;
    [SerializeField, Min(20f)] float slowZoneWidth = 420f;
    [SerializeField, Min(0.1f)] float slowZoneDuration = 1.2f;
    [SerializeField, Min(0f)] float exitDuration = 0f;

    [Header("Easing")]
    [SerializeField] AnimationCurve enterEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] AnimationCurve exitEase = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Behavior")]
    [SerializeField] bool onlyShowForLocalTurn = false;
    [SerializeField] string textPrefix = "Now Placing:";
    [SerializeField] bool requireCardChoice = true;

    [Header("Test (Play Mode)")]
    [SerializeField] bool testPlayInGame = false;
    [SerializeField] string testDisplayName = "Test Player";
    [SerializeField] bool testBypassGates = true;

    bool _targetChosen;
    uint _pendingPlacerId;

    Coroutine _playCo;
    Vector2 _startPos;

    bool _testPrev;
    bool _playingSystemMessage;
    bool _isScoringBanner;
    bool _isGameFinishedBanner;

    void Reset()
    {
        root = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        label = GetComponentInChildren<TMP_Text>(true);
    }

    void Awake()
    {
        if (!root) root = transform as RectTransform;
        _startPos = root.anchoredPosition;
        InstantHide();
    }

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        PhaseController.OnClientTargetChosen += HandleTargetChosen;
        _targetChosen = PhaseController.Instance && PhaseController.Instance.ClientHasTarget;
        RoundManager.Instance?.onRoundChangedClient?.RemoveListener(HandleClientRoundChanged);
        RoundManager.Instance?.onRoundChangedClient?.AddListener(HandleClientRoundChanged);

        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientTargetChosen -= HandleTargetChosen;
        RoundManager.Instance?.onRoundChangedClient?.RemoveListener(HandleClientRoundChanged);
    }

    void Update()
    {
        if (testPlayInGame && !_testPrev)
        {
            _testPrev = true;

            if (testBypassGates) _targetChosen = true;
            if (label) label.text = $"{textPrefix} {(!string.IsNullOrWhiteSpace(testDisplayName) ? testDisplayName : "Player")}";

            RestartPlay();
            testPlayInGame = false;
        }
        else if (!testPlayInGame && _testPrev)
        {
            _testPrev = false;
        }
    }

    void HandlePlacerChanged(uint placerNetId)
    {
        if (_playingSystemMessage)
            return;

        if (requireCardChoice && !_targetChosen)
        {
            _pendingPlacerId = placerNetId;
            InstantHide();
            return;
        }

        if (onlyShowForLocalTurn)
        {
            var me = NetworkClient.connection?.identity;
            bool myTurn = me && placerNetId != 0 && me.netId == placerNetId;
            if (!myTurn)
            {
                InstantHide();
                return;
            }
        }

        if (placerNetId == 0)
        {
            InstantHide();
            return;
        }

        label.text = $"{textPrefix} {ResolveDisplayName(placerNetId)}";
        RestartPlay();
    }

    void RestartPlay()
    {
        StopRunning();
        _playCo = StartCoroutine(Co_Play());
    }

    void StopRunning()
    {
        if (_playCo != null) StopCoroutine(_playCo);
        _playCo = null;
    }

    void InstantHide()
    {
        if (cg) cg.alpha = 0f;
        var canvas = root.GetComponentInParent<Canvas>();
        float halfW = CanvasHalfWidth(canvas, root);
        root.anchoredPosition = new Vector2(+halfW + offscreenPadding, _startPos.y);
    }

    public void PlaySystemMessage(string text, bool isScoringBanner = false, bool isGameFinishedBanner = false)
    {
        _playingSystemMessage = true;
        _isScoringBanner = isScoringBanner;
        _isGameFinishedBanner = isGameFinishedBanner;

        if (label) label.text = text;
        if (cg) cg.alpha = 1f;

        RestartPlay();
    }

    IEnumerator Co_Play()
    {
        var canvas = root.GetComponentInParent<Canvas>();
        float halfW = CanvasHalfWidth(canvas, root);
        float centerX = 0f;
        float leftX = -halfW - offscreenPadding;
        float rightX = +halfW + offscreenPadding;

        if (!backgroundRect) backgroundRect = root.GetComponentInChildren<Image>(true)?.rectTransform ?? root;
        if (!textRect && label) textRect = label.rectTransform;
        if (!textRect) textRect = root.GetComponentInChildren<RectTransform>(true);

        if (cg) cg.alpha = 1f;

        float dur1 = Mathf.Max(0.05f, enterDuration);
        float dur2 = Mathf.Max(0.1f, slowZoneDuration);
        float dur3 = exitDuration > 0f ? exitDuration : enterDuration;

        float zoneLeft = centerX - (slowZoneWidth * 0.5f);
        float zoneRight = centerX + (slowZoneWidth * 0.5f);

        root.anchoredPosition = new Vector2(0f, _startPos.y);
        backgroundRect.anchoredPosition = new Vector2(leftX, _startPos.y);
        textRect.anchoredPosition = new Vector2(leftX, _startPos.y);

        float leadPx = exitLeadPixels > 0f ? exitLeadPixels : entryLagPixels;

        float t = 0f;
        while (t < dur1)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur1);

            float xBG = Mathf.Lerp(leftX, zoneLeft, enterEase.Evaluate(u));

            float lag01 = Mathf.Clamp01(entryLagDecay.Evaluate(u));
            float lagPx = Mathf.Lerp(entryLagPixels, 0f, lag01);
            float xTX = xBG - lagPx;

            if (xTX < leftX) xTX = leftX;

            backgroundRect.anchoredPosition = new Vector2(xBG, _startPos.y);
            textRect.anchoredPosition = new Vector2(xTX, _startPos.y);
            yield return null;
        }

        t = 0f;
        while (t < dur2)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur2);
            float x = Mathf.Lerp(zoneLeft, zoneRight, u);

            backgroundRect.anchoredPosition = new Vector2(x, _startPos.y);
            textRect.anchoredPosition = new Vector2(x, _startPos.y);
            yield return null;
        }

        float te = 0f;
        while (te < dur3)
        {
            te += Time.deltaTime;
            float u = Mathf.Clamp01(te / dur3);

            float xBG = Mathf.Lerp(zoneRight, rightX, exitEase.Evaluate(u));

            float lead01 = Mathf.Clamp01(exitLeadGrowth.Evaluate(u));
            float lead = Mathf.Lerp(0f, leadPx, lead01);
            float xTX = xBG + lead;

            if (xTX > rightX) xTX = rightX;

            textRect.anchoredPosition = new Vector2(xTX, _startPos.y);
            backgroundRect.anchoredPosition = new Vector2(xBG, _startPos.y);
            yield return null;
        }

        if (cg) cg.alpha = 0f;

        if (_playingSystemMessage)
        {
            _playingSystemMessage = false;

            if (_isScoringBanner)
            {
                GridDimmerOverlay.Instance?.OnScoringBannerFinished();
                var phase = PhaseController.Instance;
                if (phase)
                {
                    phase.CmdNotifyScoringBannerFinished();
                }
            }

            if (_isGameFinishedBanner)
            {
                var reveal = FindFirstObjectByType<ScoreboardReveal>();
                if (reveal)
                {
                    reveal.ShowScoreboardNow();
                }
            }

            _isScoringBanner = false;
            _isGameFinishedBanner = false;
        }
    }

    static float CanvasHalfWidth(Canvas c, RectTransform rt)
    {
        if (!c)
        {
            var parentRt = rt && rt.parent ? rt.parent as RectTransform : null;
            if (parentRt) return Mathf.Max(1f, parentRt.rect.width) * 0.5f;
            return 960f;
        }

        var scaler = c.GetComponent<CanvasScaler>();
        if (scaler && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            float refW = Mathf.Max(1f, scaler.referenceResolution.x);
            return refW * 0.5f;
        }

        float pxW = Mathf.Max(1f, c.pixelRect.width);
        return pxW * 0.5f;
    }

    string ResolveDisplayName(uint netId)
    {
        if (NetworkClient.spawned.TryGetValue(netId, out var id) && id && id.gameObject)
        {
            var go = id.gameObject;
            var pns = go.GetComponent<PlayerNameSync>();
            if (pns != null && !string.IsNullOrWhiteSpace(pns.DisplayName))
                return pns.DisplayName.Trim();

            var chat = go.GetComponent<NetworkChat>();
            if (chat != null && !string.IsNullOrWhiteSpace(chat.DisplayName))
                return chat.DisplayName.Trim();

            return go.name;
        }
        return "Player";
    }

    void HandleTargetChosen(int col, int row, Color color)
    {
        _targetChosen = true;
        if (_pendingPlacerId != 0)
        {
            var id = _pendingPlacerId;
            _pendingPlacerId = 0;
            HandlePlacerChanged(id);
        }
    }

    void HandleClientRoundChanged(int _, uint __)
    {
        if (_playingSystemMessage)
            return;

        if (requireCardChoice)
            _targetChosen = false;

        _pendingPlacerId = CoinPlacementTurnManager.Instance
            ? CoinPlacementTurnManager.Instance.currentPlacerNetId
            : 0u;

        InstantHide();
    }

    [ContextMenu("Play Test Animation")]
    void ContextPlayTest()
    {
        if (testBypassGates) _targetChosen = true;
        if (label) label.text = $"{textPrefix} {(!string.IsNullOrWhiteSpace(testDisplayName) ? testDisplayName : "Player")}";
        RestartPlay();
    }
}
