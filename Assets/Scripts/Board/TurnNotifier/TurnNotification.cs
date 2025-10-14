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
        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        StopRunning();
    }

    void HandlePlacerChanged(uint placerNetId)
    {
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

        if (cg) cg.alpha = 1f;

        root.anchoredPosition = new Vector2(leftX, _startPos.y);

        float zoneLeft = centerX - (slowZoneWidth * 0.5f);
        float t = 0f; float dur1 = Mathf.Max(0.05f, enterDuration);
        while (t < dur1)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur1);
            float eased = enterEase.Evaluate(u);
            float x = Mathf.Lerp(leftX, zoneLeft, eased);
            root.anchoredPosition = new Vector2(x, _startPos.y);
            yield return null;
        }

        float zoneRight = centerX + (slowZoneWidth * 0.5f);
        t = 0f; float dur2 = Mathf.Max(0.1f, slowZoneDuration);
        while (t < dur2)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur2);
            float x = Mathf.Lerp(zoneLeft, zoneRight, u);
            root.anchoredPosition = new Vector2(x, _startPos.y);
            yield return null;
        }

        t = 0f; float dur3 = exitDuration > 0f ? exitDuration : enterDuration;
        while (t < dur3)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur3);
            float eased = exitEase.Evaluate(u);
            float x = Mathf.Lerp(zoneRight, rightX, eased);
            root.anchoredPosition = new Vector2(x, _startPos.y);
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
}
