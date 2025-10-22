using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ScorePop : MonoBehaviour
{
    public static ScorePop Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] RectTransform uiParent;
    [SerializeField] TMP_Text scoreTextPrefab;

    [Header("Phases (durations)")]
    [SerializeField, Min(0f)] float popDuration = 0.22f;
    [SerializeField, Min(0f)] float holdDuration = 0.35f;
    [SerializeField, Min(0f)] float flyDuration = 0.55f;

    [Header("Pop & Hold Style")]
    [SerializeField] Vector2 rise = new Vector2(0f, 38f);
    [SerializeField] float startScale = 0.65f;
    [SerializeField] float popScale = 1.05f;
    [SerializeField] AnimationCurve popCurve = null;
    [SerializeField] AnimationCurve holdCurve = null;

    [Header("Flight Style")]
    [SerializeField] AnimationCurve flyCurve = null;
    [Range(0f, 0.8f)] public float flyStretch = 0.28f;
    [SerializeField] AnimationCurve globalAlphaCurve = null;

    [Header("Spawn Layer Override")]
    [SerializeField] RectTransform spawnLayer;

    int _targetCol = -1, _targetRow = -1;
    int _pointsAtExact = 3;

    static readonly Dictionary<string, RectTransform> _bannerTargets = new();

    void Awake()
    {
        Instance = this;
        if (!uiParent) uiParent = transform as RectTransform;

        if (popCurve == null) popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (holdCurve == null) holdCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (flyCurve == null) flyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    void OnEnable()
    {
        PhaseController.OnClientTargetChosen += OnClientTargetChosen;
        RebuildBannerTargets();
    }

    void OnDisable()
    {
        PhaseController.OnClientTargetChosen -= OnClientTargetChosen;
    }

    void OnClientTargetChosen(int col, int row, Color _)
    {
        _targetCol = col;
        _targetRow = row;
    }

    public void ConfigureFromPhase(int pointsAtExact)
    {
        _pointsAtExact = Mathf.Max(0, pointsAtExact);
    }

    public static void RebuildBannerTargets()
    {
        _bannerTargets.Clear();
        var entries = Object.FindObjectsByType<ScoreBannerEntry>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (!e) continue;
            var name = e.OwnerName;
            var rt = e.FlyTargetAnchor ? e.FlyTargetAnchor : e.GetComponent<RectTransform>();
            if (!string.IsNullOrEmpty(name) && rt) _bannerTargets[name] = rt;
        }
    }

    public static void TrySpawnForCell(int col, int row, RectTransform cellRect)
    {
        var inst = Instance;
        if (!inst || !cellRect || inst._targetCol < 0 || inst._targetRow < 0) return;

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

        int targetRowBoard = NormalizeRowToBoard(inst._targetRow, board);
        int rowBoardForCoin = NormalizeRowToBoard(row, board);
        int cellsAway = Mathf.Max(Mathf.Abs(col - inst._targetCol), Mathf.Abs(rowBoardForCoin - targetRowBoard)); // fixed
        int points = Mathf.Max(0, inst._pointsAtExact - cellsAway);
        if (points <= 0) return;

        string ownerName = null;
        if (NetworkClient.active && NetworkClient.spawned.TryGetValue(coinNetId, out var coinIdentity) && coinIdentity)
        {
            var coin = coinIdentity.GetComponent<NetworkCoin>();
            if (coin != null && coin.ownerNetId != 0)
                RosterStore.TryGetNameByNetId(coin.ownerNetId, out ownerName);
        }
        if (string.IsNullOrWhiteSpace(ownerName)) ownerName = "Unknown";

        if (_bannerTargets.Count == 0) RebuildBannerTargets();
        _bannerTargets.TryGetValue(ownerName, out var bannerAnchor);
        if (!bannerAnchor) { Debug.LogWarning($"[ScoreFX] Missing banner for '{ownerName}'"); return; }

        var parent = inst.spawnLayer ? inst.spawnLayer : (cellRect.parent as RectTransform);
        if (!parent) return;

        if (!inst.scoreTextPrefab) return;
        var text = Instantiate(inst.scoreTextPrefab, parent);
        text.text = $"+{points}";
        var rt = text.rectTransform;
        rt.SetAsLastSibling();
        text.alpha = 0f;

        var spawnCanvas = parent.GetComponentInParent<Canvas>();
        Camera cam = spawnCanvas ? spawnCanvas.worldCamera : null;

        Vector3 cellWorld = cellRect.TransformPoint(cellRect.rect.center);
        Vector2 startLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
            RectTransformUtility.WorldToScreenPoint(cam, cellWorld),
            cam, out startLocal);

        Vector2 midLocal = startLocal + inst.rise;

        Vector3 bannerWorld = bannerAnchor.TransformPoint((Vector2)bannerAnchor.rect.center);
        Vector2 targetLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
            RectTransformUtility.WorldToScreenPoint(cam, bannerWorld),
            cam, out targetLocal);

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = startLocal;
        rt.localScale = Vector3.one * inst.startScale;

        inst.StartCoroutine(inst.CoPopHoldFly(text, rt, startLocal, midLocal, targetLocal, bannerAnchor, ownerName));
    }

    IEnumerator CoPopHoldFly(TMP_Text t, RectTransform rt, Vector2 start, Vector2 mid, Vector2 target, RectTransform bannerAnchor, string ownerName)
    {
        float tPop = 0f; float dPop = Mathf.Max(0.0001f, popDuration);
        while (tPop < dPop && t)
        {
            tPop += Time.deltaTime; float u = Mathf.Clamp01(tPop / dPop);
            float e = popCurve != null ? popCurve.Evaluate(u) : u;

            float s = Mathf.Lerp(startScale, popScale, e);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, mid, e);

            float a = (globalAlphaCurve != null) ? globalAlphaCurve.Evaluate(u * 0.6f) : Mathf.SmoothStep(0f, 1f, u);
            t.alpha = a;

            yield return null;
        }

        float tHold = 0f; float dHold = Mathf.Max(0f, holdDuration);
        Vector2 holdPos = rt.anchoredPosition;
        while (tHold < dHold && t)
        {
            tHold += Time.deltaTime; float u = Mathf.Clamp01(tHold / Mathf.Max(0.0001f, dHold));
            float e = holdCurve != null ? holdCurve.Evaluate(u) : u;

            rt.anchoredPosition = Vector2.LerpUnclamped(holdPos, mid, e);
            rt.localScale = Vector3.Lerp(Vector3.one * popScale, Vector3.one, e);
            t.alpha = 1f;

            yield return null;
        }

        float tFly = 0f; float dFly = Mathf.Max(0.0001f, flyDuration);
        Vector2 flyStart = rt.anchoredPosition;

        ScoreBannerEntry pulse = bannerAnchor ? bannerAnchor.GetComponentInParent<ScoreBannerEntry>() : null;

        while (tFly < dFly && t)
        {
            tFly += Time.deltaTime; float u = Mathf.Clamp01(tFly / dFly);
            float e = (flyCurve != null) ? flyCurve.Evaluate(u) : u;

            Vector2 pos = Vector2.LerpUnclamped(flyStart, target, e);
            rt.anchoredPosition = pos;

            Vector2 dir = (target - flyStart);
            float len = dir.magnitude + 0.0001f;
            Vector2 nd = dir / len;
            float dirX = Mathf.Abs(nd.x);
            float dirY = Mathf.Abs(nd.y);
            float stretchNow = flyStretch * Mathf.SmoothStep(0f, 1f, e) * Mathf.SmoothStep(1f, 0f, Mathf.Abs(2f * e - 1f));
            float sx = 1f + stretchNow * dirX * 1.5f;
            float sy = 1f - stretchNow * 0.5f * dirX;
            if (dirY > dirX)
            {
                sx = 1f - stretchNow * 0.5f * dirY;
                sy = 1f + stretchNow * dirY * 1.5f;
            }
            rt.localScale = new Vector3(sx, sy, 1f);

            float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 1.2f - 0.15f));
            t.alpha = a;

            yield return null;
        }

        if (t) { rt.anchoredPosition = target; t.alpha = 0f; }

        if (pulse) pulse.PulseGlow();

        if (t) Destroy(t.gameObject);
    }

    public static int NormalizeRowToBoard(int rawRowBottomOrigin, BoardSpotsNet b)
    {
        return b.AStartsAtTop ? (b.GridRows - 1 - rawRowBottomOrigin) : rawRowBottomOrigin;
    }
}
