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
    [SerializeField, Min(0f)] float textOffset = 0.2f;

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

    Coroutine _playCo;
    Vector2 _startPos;

    bool _targetChosenThisRound = false;


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
        PhaseController.OnClientTargetChosen += ArmForThisRound;              // NEW
        RoundManager.Instance?.onRoundChangedClient.AddListener(OnRoundChangedClient_Reset); // NEW

        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientTargetChosen -= ArmForThisRound;              // NEW
        if (RoundManager.Instance)
            RoundManager.Instance.onRoundChangedClient.RemoveListener(OnRoundChangedClient_Reset); // NEW
    }

    void HandlePlacerChanged(uint placerNetId)
    {
        if (!_targetChosenThisRound) { InstantHide(); return; }

        if (onlyShowForLocalTurn)
        {
            var me = NetworkClient.connection?.identity;
            bool myTurn = me && placerNetId != 0 && me.netId == placerNetId;
            if (!myTurn) { InstantHide(); return; }
        }

        if (placerNetId == 0) { InstantHide(); return; }

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
        float dur3 = (exitDuration > 0f ? exitDuration : enterDuration);

        float zoneLeft = centerX - (slowZoneWidth * 0.5f);
        float zoneRight = centerX + (slowZoneWidth * 0.5f);

        root.anchoredPosition = new Vector2(0f, _startPos.y);
        backgroundRect.anchoredPosition = new Vector2(leftX, _startPos.y);
        textRect.anchoredPosition = new Vector2(leftX, _startPos.y);

        float t = 0f;
        while (t < dur1)
        {
            t += Time.deltaTime;
            float uBG = Mathf.Clamp01(t / dur1);
            float uTX = Mathf.Clamp01((t - textOffset) / (dur1 - textOffset + 1e-6f));

            float xBG = Mathf.Lerp(leftX, zoneLeft, enterEase.Evaluate(uBG));
            float xTX = Mathf.Lerp(leftX, zoneLeft, enterEase.Evaluate(uTX));

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

        float exitElapsed = 0f;
        while (exitElapsed < (dur3 + textOffset))
        {
            exitElapsed += Time.deltaTime;

            float uTX = Mathf.Clamp01((exitElapsed + textOffset) / dur3);
            float uBG = Mathf.Clamp01(exitElapsed / dur3);

            float xTX = Mathf.Lerp(zoneRight, rightX, exitEase.Evaluate(uTX));
            float xBG = Mathf.Lerp(zoneRight, rightX, exitEase.Evaluate(uBG));

            textRect.anchoredPosition = new Vector2(xTX, _startPos.y);
            backgroundRect.anchoredPosition = new Vector2(xBG, _startPos.y);
            yield return null;
        }

        if (cg) cg.alpha = 0f;
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

    void ArmForThisRound() => _targetChosenThisRound = true;

    void OnRoundChangedClient_Reset(int _, uint __)
    {
        _targetChosenThisRound = false;
        InstantHide();
    }
}
