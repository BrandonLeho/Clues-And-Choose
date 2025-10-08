using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TurnNotification : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RectTransform root;
    [SerializeField] RectTransform backgroundRT;
    [SerializeField] RectTransform textRT;
    [SerializeField] TMP_Text textLabel;

    [Header("Direction")]
    [SerializeField] bool fromRight = false;

    [Header("Timing")]
    [SerializeField, Min(0f)] float bgStartDelay = 0.00f;
    [SerializeField, Min(0f)] float textStartDelay = 0.12f;
    [SerializeField, Min(0f)] float driftDuration = 0.75f;

    [Header("Speeds")]
    [SerializeField, Min(1f)] float enterSpeed = 1400f;
    [SerializeField, Range(0.01f, 0.75f)] float driftSpeedFactor = 0.15f;

    [Header("Positioning")]
    [SerializeField, Min(0f)] float edgeMargin = 80f;
    [SerializeField] float centerX = 0f;
    [SerializeField] float textCenterOffset = 0f;

    [Header("Behavior")]
    [SerializeField] bool showForEveryone = true;

    Canvas _canvas;
    Coroutine _animCR;
    bool _sawCycleEvent = false;

    void Reset()
    {
        root = GetComponent<RectTransform>();
        if (root && root.childCount >= 2)
        {
            backgroundRT = root.GetChild(0) as RectTransform;
            textRT = root.GetChild(1) as RectTransform;
            textLabel = root.GetChild(1).GetComponent<TMP_Text>();
        }
    }

    void Awake()
    {
        if (!root) root = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        SetInstantHidden();
    }

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        PlacingPhaseController.OnClientCycleStarted += HandleCycleStarted;

        if (CoinPlacementTurnManager.Instance)
        {
            if (!_sawCycleEvent) SetFromRight(false);
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
        }
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PlacingPhaseController.OnClientCycleStarted -= HandleCycleStarted;
    }

    public void SetFromRight(bool value) => fromRight = value;

    void HandlePlacerChanged(uint placerNetId)
    {
        if (!_sawCycleEvent) SetFromRight(false);

        if (placerNetId == 0)
        {
            StopAnimIfAny();
            SetInstantHidden();
            return;
        }

        if (!showForEveryone && !CoinPlacementTurnManager.IsLocalPlayersTurn())
        {
            StopAnimIfAny();
            SetInstantHidden();
            return;
        }

        string display = BuildDisplayString(placerNetId);
        ShowTurn(display);
    }

    string BuildDisplayString(uint placerNetId)
    {
        string name = ResolvePlayerNameClient(placerNetId);
        if (string.IsNullOrWhiteSpace(name))
            name = $"Player {placerNetId}";
        return $"{name}'s Turn";
    }

    string ResolvePlayerNameClient(uint netId)
    {
        if (NetworkClient.spawned == null) return null;
        if (!NetworkClient.spawned.TryGetValue(netId, out var identity) || !identity)
            return null;

        var go = identity.gameObject;
        var pns = go.GetComponent<PlayerNameSync>();
        if (pns && !string.IsNullOrWhiteSpace(pns.DisplayName)) return pns.DisplayName.Trim();

        var chat = go.GetComponent<NetworkChat>();
        if (chat && !string.IsNullOrWhiteSpace(chat.DisplayName)) return chat.DisplayName.Trim();

        return go.name;
    }

    public void ShowTurn(string message)
    {
        textLabel.text = message;
        StopAnimIfAny();
        _animCR = StartCoroutine(AnimateRoutine());
    }

    void StopAnimIfAny()
    {
        if (_animCR != null) StopCoroutine(_animCR);
        _animCR = null;
    }

    void SetInstantHidden()
    {
        if (!root || !backgroundRT || !textRT) return;

        float startX = OffscreenX(+1);
        Vector2 p = new Vector2(startX, root.anchoredPosition.y);

        backgroundRT.anchoredPosition = p;
        textRT.anchoredPosition = p;

        backgroundRT.gameObject.SetActive(false);
        textRT.gameObject.SetActive(false);
    }

    IEnumerator AnimateRoutine()
    {
        if (!root || !backgroundRT || !textRT) yield break;

        backgroundRT.gameObject.SetActive(true);
        textRT.gameObject.SetActive(true);

        int dir = fromRight ? -1 : +1;
        dir = fromRight ? -1 : +1;

        float startX = OffscreenX(dir * -1);
        float endCenterBgX = centerX;

        float endCenterTextX = centerX + textCenterOffset;

        float exitX = OffscreenX(dir);

        Vector2 startPos = new Vector2(startX, root.anchoredPosition.y);
        backgroundRT.anchoredPosition = startPos;
        textRT.anchoredPosition = startPos;

        float bgDelay = Mathf.Max(0f, bgStartDelay);
        float txtDelay = Mathf.Max(0f, textStartDelay);

        yield return MoveX(backgroundRT, startX, endCenterBgX, enterSpeed, bgDelay);

        yield return MoveX(textRT, textRT.anchoredPosition.x, endCenterTextX, enterSpeed, Mathf.Max(0f, txtDelay));

        float driftSpeed = Mathf.Max(1f, enterSpeed * driftSpeedFactor);
        float driftTarget = endCenterTextX + (dir * Mathf.Max(50f, root.rect.width * 0.08f));
        float t = 0f;
        float driftTime = Mathf.Max(0f, driftDuration);
        while (t < driftTime)
        {
            t += Time.unscaledDeltaTime;
            textRT.anchoredPosition = MoveTowardsX(textRT.anchoredPosition, driftTarget, driftSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        yield return MoveBothExit(backgroundRT, textRT, exitX, enterSpeed);

        backgroundRT.gameObject.SetActive(false);
        textRT.gameObject.SetActive(false);
        _animCR = null;
    }

    IEnumerator MoveX(RectTransform rt, float fromX, float toX, float speed, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (rt.anchoredPosition.x != fromX)
            rt.anchoredPosition = new Vector2(fromX, rt.anchoredPosition.y);

        float epsilon = 0.5f;
        while (Mathf.Abs(rt.anchoredPosition.x - toX) > epsilon)
        {
            float step = speed * Time.unscaledDeltaTime;
            rt.anchoredPosition = MoveTowardsX(rt.anchoredPosition, toX, step);
            yield return null;
        }
        rt.anchoredPosition = new Vector2(toX, rt.anchoredPosition.y);
    }

    IEnumerator MoveBothExit(RectTransform bg, RectTransform txt, float exitX, float speed)
    {
        float epsilon = 0.5f;
        while (Mathf.Abs(bg.anchoredPosition.x - exitX) > epsilon ||
               Mathf.Abs(txt.anchoredPosition.x - exitX) > epsilon)
        {
            float step = speed * Time.unscaledDeltaTime;
            bg.anchoredPosition = MoveTowardsX(bg.anchoredPosition, exitX, step);
            txt.anchoredPosition = MoveTowardsX(txt.anchoredPosition, exitX, step);
            yield return null;
        }
        bg.anchoredPosition = new Vector2(exitX, bg.anchoredPosition.y);
        txt.anchoredPosition = new Vector2(exitX, txt.anchoredPosition.y);
    }

    Vector2 MoveTowardsX(Vector2 p, float targetX, float maxDelta)
        => new Vector2(Mathf.MoveTowards(p.x, targetX, maxDelta), p.y);

    float OffscreenX(int dir)
    {
        RectTransform canvasRT = _canvas ? _canvas.transform as RectTransform : null;
        float halfCanvas = canvasRT ? canvasRT.rect.width * 0.5f : Screen.width * 0.5f;
        float halfSelf = root ? Mathf.Abs(root.rect.width) * 0.5f : 200f;
        return dir * (halfCanvas + halfSelf + edgeMargin);
    }

    void HandleCycleStarted(bool reversed)
    {
        _sawCycleEvent = true;
        SetFromRight(reversed);
    }
}
