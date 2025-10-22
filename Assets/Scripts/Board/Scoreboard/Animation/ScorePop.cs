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
    [SerializeField] float lifetime = 0.9f;
    [SerializeField] Vector2 rise = new Vector2(0f, 40f);
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] float startScale = 0.7f;
    [SerializeField] float endScale = 1.2f;

    [Header("Timing")]
    [SerializeField, Min(0f)] float popUpDuration = 0.25f;
    [SerializeField, Min(0f)] float holdDuration = 0.20f;
    [SerializeField, Min(0f)] float flyDuration = 0.45f;

    [Header("Flight")]
    [SerializeField] AnimationCurve popEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] AnimationCurve holdEase = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] AnimationCurve flyEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField, Range(0f, 0.5f)] float stretchAmount = 0.18f;
    [SerializeField] float endFadeOut = 0.12f;


    int _targetCol = -1, _targetRow = -1;
    int _pointsAtExact = 3;

    void Awake()
    {
        Instance = this;
        if (!uiParent) uiParent = transform as RectTransform;
    }

    void OnEnable()
    {
        PhaseController.OnClientTargetChosen += OnClientTargetChosen;
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
        Debug.Log(coinNetId + " " + spotIndex);

        int targetRowBoard = NormalizeRowToBoard(Instance._targetRow, board);
        int cellsAway = Mathf.Max(Mathf.Abs(col - Instance._targetCol), Mathf.Abs(targetRowBoard - targetRowBoard));
        int points = Mathf.Max(0, Instance._pointsAtExact - cellsAway);
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

        Debug.Log($"[ScoreFX] +{points} → {ownerName} @ cell ({col + 1},{row + 1})");

        if (!inst.scoreTextPrefab) return;

        var parent = cellRect.parent as RectTransform;
        if (!parent) return;

        var spawned = Instantiate(inst.scoreTextPrefab, parent);
        spawned.text = $"+{points}";
        var rt = spawned.rectTransform;

        rt.anchorMin = cellRect.anchorMin;
        rt.anchorMax = cellRect.anchorMax;
        rt.pivot = cellRect.pivot;
        rt.anchoredPosition = cellRect.anchoredPosition;
        rt.localScale = Vector3.one * inst.startScale;
        rt.SetAsLastSibling();
        spawned.alpha = 0f;

        RectTransform bannerAnchor = ResolveBannerAnchor(ownerName);
        if (!bannerAnchor)
        {
            inst.StartCoroutine(inst.CoPop(spawned));
            return;
        }

        Vector2 targetPos = WorldToLocalInParent(parent, bannerAnchor);

        inst.StartCoroutine(inst.CoPopHoldFly(spawned, rt.anchoredPosition, rt.anchoredPosition + inst.rise, targetPos));
    }

    IEnumerator CoPop(TMP_Text t)
    {
        if (!t) yield break;
        var rt = t.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + rise;

        float d = Mathf.Max(0.001f, lifetime);
        float time = 0f;

        while (time < d && t)
        {
            time += Time.deltaTime;
            float u = Mathf.Clamp01(time / d);
            float e = ease != null ? ease.Evaluate(u) : u;

            float s = Mathf.Lerp(startScale, endScale, e);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
            t.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(u * 2f, 1f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, u - 0.5f) * 2f));

            yield return null;
        }

        if (t) Destroy(t.gameObject);
    }

    public static int NormalizeRowToBoard(int rawRowBottomOrigin, BoardSpotsNet b)
    {
        return b.AStartsAtTop ? (b.GridRows - 1 - rawRowBottomOrigin) : rawRowBottomOrigin;
    }

    static RectTransform ResolveBannerAnchor(string ownerName)
    {
        if (string.IsNullOrWhiteSpace(ownerName)) return null;
        ScoreBannerEntry.NameToScoreAnchor.TryGetValue(ownerName, out var rt);
        return rt;
    }

    static Vector2 WorldToLocalInParent(RectTransform parent, RectTransform target)
    {
        if (!parent || !target) return Vector2.zero;

        var root = parent.root as RectTransform;
        var canvas = root ? root.GetComponentInParent<Canvas>() : parent.GetComponentInParent<Canvas>();
        var cam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, target.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out var local);
        return local;
    }

    IEnumerator CoPopHoldFly(TMP_Text t, Vector2 popStart, Vector2 popEnd, Vector2 target)
    {
        if (!t) yield break;
        var rt = t.rectTransform;

        {
            float d = Mathf.Max(0.0001f, popUpDuration);
            float time = 0f;
            while (time < d && t)
            {
                time += Time.deltaTime;
                float u = Mathf.Clamp01(time / d);
                float e = popEase != null ? popEase.Evaluate(u) : u;

                float s = Mathf.Lerp(startScale, 1f, e);
                rt.localScale = new Vector3(s, s, 1f);
                rt.anchoredPosition = Vector2.LerpUnclamped(popStart, popEnd, e);
                t.alpha = Mathf.SmoothStep(0f, 1f, u);

                yield return null;
            }
            if (!t) yield break;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = popEnd;
            t.alpha = 1f;
        }

        {
            float d = Mathf.Max(0f, holdDuration);
            float time = 0f;
            Vector2 holdPos = rt.anchoredPosition;
            while (time < d && t)
            {
                time += Time.deltaTime;
                float u = d > 0f ? Mathf.Clamp01(time / d) : 1f;
                float e = holdEase != null ? holdEase.Evaluate(u) : u;

                rt.anchoredPosition = Vector2.LerpUnclamped(holdPos, holdPos, e);
                rt.localScale = Vector3.one;
                t.alpha = 1f;

                yield return null;
            }
            if (!t) yield break;
        }

        {
            float d = Mathf.Max(0.0001f, flyDuration);
            float time = 0f;
            Vector2 start = rt.anchoredPosition;

            while (time < d && t)
            {
                time += Time.deltaTime;
                float u = Mathf.Clamp01(time / d);
                float e = flyEase != null ? flyEase.Evaluate(u) : u;

                Vector2 pos = Vector2.LerpUnclamped(start, target, e);
                rt.anchoredPosition = pos;

                float stretch = stretchAmount * Mathf.Sin(u * Mathf.PI);
                float sx = 1f + stretch;
                float sy = 1f - 0.25f * stretch;
                rt.localScale = new Vector3(sx, sy, 1f);

                if (endFadeOut > 0f)
                {
                    float remain = Mathf.Max(0f, d - time);
                    float fade = (remain <= endFadeOut) ? Mathf.InverseLerp(0f, endFadeOut, remain) : 1f;
                    t.alpha = fade;
                }

                yield return null;
            }
        }

        if (t) Destroy(t.gameObject);
    }
}
