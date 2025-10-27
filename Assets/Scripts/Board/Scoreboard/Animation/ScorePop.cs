using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;

public sealed class ScorePop : MonoBehaviour
{
    public static ScorePop Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] RectTransform uiParent;
    [SerializeField] TMP_Text scoreTextPrefab;

    [Header("Pop/Hold/Fly Timing")]
    [SerializeField, Min(0.01f)] float popDuration = 0.28f;

    [Tooltip("Time the text stays readable before flying.")]
    [SerializeField, Min(0f)] float holdDuration = 0.35f;

    [Tooltip("Flight duration to the banner.")]
    [SerializeField, Min(0.05f)] float flyDuration = 0.55f;

    [Header("Pop Movement/Scale")]
    [SerializeField] Vector2 rise = new Vector2(0f, 42f);
    [SerializeField] AnimationCurve popEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] float startScale = 0.65f;
    [SerializeField] float readableScale = 1.0f;

    [Header("Flight Motion")]
    [SerializeField] float flyArcPixels = 36f;
    [SerializeField] AnimationCurve flyEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] Vector2 bannerOffset = new Vector2(0f, 10f);

    [Header("Stretch During Flight")]
    [SerializeField, Range(0f, 0.6f)] float flyStretchAmount = 0.18f;
    [SerializeField, Range(0f, 0.6f)] float flySquashAmount = 0.08f;

    [Header("Fade During Flight")]
    [SerializeField] bool fadeDuringFlight = true;
    [SerializeField, Range(0f, 1f)] float fadeStart = 0.6f;
    [SerializeField] AnimationCurve fadeEase = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Flight Scaling")]
    [SerializeField] bool scaleDownDuringFlight = true;
    [SerializeField, Range(0.1f, 2f)] float flightEndScale = 0.75f;
    [SerializeField] AnimationCurve flightScaleEase = AnimationCurve.Linear(0, 0, 1, 1);


    [Header("Layer Override")]
    [SerializeField] RectTransform spawnLayer;

    [Header("Debug")]
    [SerializeField] bool debugLogs = false;

    int _targetCol = -1, _targetRow = -1;
    int _pointsAtExact = 3;

    public static event System.Action<string, int> OnScoreFlyArrived;

    public static float TotalFlightSeconds => Instance ? (Instance.popDuration + Instance.holdDuration + Instance.flyDuration) : 1f;

    void Awake()
    {
        Instance = this;
        if (!uiParent) uiParent = transform as RectTransform;
    }

    void OnEnable() { PhaseController.OnClientTargetChosen += OnClientTargetChosen; }
    void OnDisable() { PhaseController.OnClientTargetChosen -= OnClientTargetChosen; }

    void OnClientTargetChosen(int col, int row, Color _) { _targetCol = col; _targetRow = row; }
    public void ConfigureFromPhase(int pointsAtExact) { _pointsAtExact = Mathf.Max(0, pointsAtExact); }

    public static void TrySpawnForCell(int col, int row, RectTransform cellRect)
    {
        var inst = Instance;
        if (!inst || !cellRect) return;
        if (inst._targetCol < 0 || inst._targetRow < 0) return;

        uint coinNetId = 0;
        int spotIndex = -1;
        var board = BoardSpotsNet.Instance;
        if (board != null)
        {
            int rowBoard = NormalizeRowToBoard(row, board);
            foreach (var kv in board.occupancy)
            {
                if (board.TryGetSpotCoord(kv.Key, out int c, out int r))
                {
                    if (c == col && r == rowBoard)
                    {
                        spotIndex = kv.Key;
                        coinNetId = kv.Value;
                        break;
                    }
                }
            }
        }

        if (coinNetId == 0) return;
        if (inst.debugLogs) Debug.Log($"[ScorePop] coinNetId={coinNetId} spotIndex={spotIndex}");

        int targetRowBoard = NormalizeRowToBoard(inst._targetRow, board);
        int cellsAway = Mathf.Max(Mathf.Abs(col - inst._targetCol), Mathf.Abs(targetRowBoard - targetRowBoard));
        int points = Mathf.Max(0, inst._pointsAtExact - cellsAway);
        if (points <= 0) return;

        string ownerName = null;
        if (NetworkClient.active && NetworkClient.spawned.TryGetValue(coinNetId, out var coinId) && coinId)
        {
            var coin = coinId.GetComponent<NetworkCoin>();
            if (coin != null && coin.ownerNetId != 0)
            {
                RosterStore.TryGetNameByNetId(coin.ownerNetId, out ownerName);
            }
        }
        if (string.IsNullOrWhiteSpace(ownerName)) ownerName = "Unknown";

        if (!inst.scoreTextPrefab) return;

        var parent = inst.spawnLayer ? inst.spawnLayer : (cellRect.parent as RectTransform);
        if (!parent) return;

        var text = Instantiate(inst.scoreTextPrefab, parent);
        text.text = $"+{points}";
        var rt = text.rectTransform;

        Vector2 startAnchored = inst.AnchoredAtCellCenter(cellRect, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = startAnchored;
        rt.localScale = Vector3.one * inst.startScale;
        text.alpha = 0f;
        rt.SetAsLastSibling();

        RectTransform targetRt;
        if (!ScoreBannerEntry.TryGetFlyTargetFor(ownerName, out targetRt))
        {
            if (inst.debugLogs) Debug.LogWarning($"[ScorePop] No banner for '{ownerName}'. Falling back to pop.");
            inst.StartCoroutine(inst.CoPopOnly(text));
            return;
        }

        Vector2 endAnchored = inst.AnchoredFromWorld(targetRt.TransformPoint(targetRt.rect.center), parent) + inst.bannerOffset;

        inst.StartCoroutine(inst.CoPopHoldFly(text, startAnchored, endAnchored, ownerName, points));

    }

    IEnumerator CoPopOnly(TMP_Text t)
    {
        if (!t) yield break;
        var rt = t.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + rise;

        float time = 0f;
        float d = Mathf.Max(0.001f, popDuration + holdDuration * 0.5f);

        while (time < d && t)
        {
            time += Time.deltaTime;
            float u = Mathf.Clamp01(time / d);
            float e = popEase != null ? popEase.Evaluate(u) : u;

            float s = Mathf.Lerp(startScale, readableScale, e);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
            t.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(u * 2f, 1f));
            yield return null;
        }
        if (t) Destroy(t.gameObject);
    }

    IEnumerator CoPopHoldFly(TMP_Text t, Vector2 startAnchored, Vector2 endAnchored, string ownerName, int points)
    {
        if (!t) yield break;
        var rt = t.rectTransform;

        Vector2 popEnd = startAnchored + rise;
        float t1 = 0f;
        while (t1 < popDuration && t)
        {
            t1 += Time.deltaTime;
            float u = Mathf.Clamp01(t1 / Mathf.Max(0.0001f, popDuration));
            float e = popEase != null ? popEase.Evaluate(u) : u;

            float s = Mathf.Lerp(startScale, readableScale, e);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = Vector2.LerpUnclamped(startAnchored, popEnd, e);
            t.alpha = Mathf.SmoothStep(0f, 1f, u);
            yield return null;
        }

        float t2 = 0f;
        while (t2 < holdDuration && t)
        {
            t2 += Time.deltaTime;
            float settle = 1f + 0.03f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t2 / holdDuration));
            rt.localScale = new Vector3(readableScale * settle, readableScale * settle, 1f);
            rt.anchoredPosition = popEnd;
            t.alpha = 1f;
            yield return null;
        }
        if (!t) yield break;
        rt.localScale = Vector3.one * readableScale;

        Vector2 p0 = popEnd;
        Vector2 p2 = endAnchored;
        Vector2 p1 = Vector2.Lerp(p0, p2, 0.5f) + Vector2.up * flyArcPixels;

        float t3 = 0f;
        while (t3 < flyDuration && t)
        {
            t3 += Time.deltaTime;
            float u = Mathf.Clamp01(t3 / Mathf.Max(0.0001f, flyDuration));
            float e = flyEase != null ? flyEase.Evaluate(u) : u;

            Vector2 a = Vector2.Lerp(p0, p1, e);
            Vector2 b = Vector2.Lerp(p1, p2, e);
            Vector2 pos = Vector2.Lerp(a, b, e);
            rt.anchoredPosition = pos;

            float baseScale = readableScale;
            if (scaleDownDuringFlight)
            {
                float sT = flightScaleEase != null ? flightScaleEase.Evaluate(e) : e;
                baseScale = Mathf.Lerp(readableScale, flightEndScale, sT);
            }

            float k = Mathf.Sin(e * Mathf.PI);
            float wide = baseScale * (1f + flyStretchAmount * k);
            float tall = baseScale * (1f - flySquashAmount * k);
            rt.localScale = new Vector3(wide, tall, 1f);

            if (fadeDuringFlight)
            {
                float fadeT = Mathf.Clamp01((e - fadeStart) / Mathf.Max(0.0001f, 1f - fadeStart));
                float fade = 1f - (fadeEase != null ? fadeEase.Evaluate(fadeT) : fadeT);
                t.alpha = fade;
            }
            else
            {
                t.alpha = 1f;
            }

            yield return null;
        }

        OnScoreFlyArrived?.Invoke(ownerName, points);
        if (PhaseController.Instance) PhaseController.Instance.CmdReportScoreArrival(ownerName, points);
        if (t) Destroy(t.gameObject);

    }

    Vector2 AnchoredAtCellCenter(RectTransform cell, RectTransform targetParent)
    {
        var worldCenter = cell.TransformPoint(cell.rect.center);
        return AnchoredFromWorld(worldCenter, targetParent);
    }

    Vector2 AnchoredFromWorld(Vector3 worldPos, RectTransform targetParent)
    {
        var canvas = targetParent.GetComponentInParent<Canvas>();
        var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screen, cam, out var local);
        return local;
    }

    public static int NormalizeRowToBoard(int rawRowBottomOrigin, BoardSpotsNet b)
    {
        return b.AStartsAtTop ? (b.GridRows - 1 - rawRowBottomOrigin) : rawRowBottomOrigin;
    }

    public void SetSpawnLayer(RectTransform newLayer) => spawnLayer = newLayer;

    public static void TrySpawnFromCellToBanner(string ownerName, int points, RectTransform cellRect, float scaleMultiplier = 1f)
    {
        var inst = Instance;
        if (!inst || !cellRect || string.IsNullOrWhiteSpace(ownerName) || points <= 0) return;
        if (!inst.scoreTextPrefab) return;

        var parent = inst.spawnLayer ? inst.spawnLayer : (cellRect.parent as RectTransform);
        if (!parent) return;

        var text = Instantiate(inst.scoreTextPrefab, parent);
        text.text = $"+{points}";
        var rt = text.rectTransform;

        Vector2 startAnchored = inst.AnchoredAtCellCenter(cellRect, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = startAnchored;
        rt.localScale = Vector3.one * inst.startScale * Mathf.Max(0.01f, scaleMultiplier);
        text.alpha = 0f;
        rt.SetAsLastSibling();

        if (!ScoreBannerEntry.TryGetFlyTargetFor(ownerName, out var targetRt))
        {
            if (inst.debugLogs) Debug.LogWarning($"[ScorePop] No banner for '{ownerName}' (bonus).");
            inst.StartCoroutine(inst.CoPopOnly(text));
            return;
        }

        Vector2 endAnchored = inst.AnchoredFromWorld(targetRt.TransformPoint(targetRt.rect.center), parent) + inst.bannerOffset;

        inst.StartCoroutine(inst.CoPopHoldFly_WithScale(text, startAnchored, endAnchored, ownerName, points, scaleMultiplier));
    }

    IEnumerator CoPopHoldFly_WithScale(TMP_Text t, Vector2 startAnchored, Vector2 endAnchored, string ownerName, int points, float scaleMul)
    {
        float oldStart = startScale;
        float oldReadable = readableScale;
        startScale *= scaleMul;
        readableScale *= scaleMul;

        yield return CoPopHoldFly(t, startAnchored, endAnchored, ownerName, points);

        startScale = oldStart;
        readableScale = oldReadable;
    }

}
