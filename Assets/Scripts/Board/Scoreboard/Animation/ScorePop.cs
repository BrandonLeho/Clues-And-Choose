using System.Collections;
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

    [Header("Layering")]
    [SerializeField] RectTransform spawnLayer;
    [SerializeField] bool setAsLastSibling = true;

    int _targetCol = -1, _targetRow = -1;
    int _pointsAtExact = 3;

    void Awake()
    {
        Instance = this;
        if (!uiParent) uiParent = transform as RectTransform;
    }

    void OnEnable() { PhaseController.OnClientTargetChosen += OnClientTargetChosen; }
    void OnDisable() { PhaseController.OnClientTargetChosen -= OnClientTargetChosen; }

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

        int targetRowBoard = NormalizeRowToBoard(inst._targetRow, board);
        int thisRowBoard = NormalizeRowToBoard(row, board);

        int cellsAway = Mathf.Max(Mathf.Abs(col - inst._targetCol), Mathf.Abs(thisRowBoard - targetRowBoard));
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

        Debug.Log($"[ScoreFX] +{points} → {ownerName} @ cell ({col + 1},{row + 1})");
        if (!inst.scoreTextPrefab) return;

        var targetLayer = inst.spawnLayer ? inst.spawnLayer : inst.uiParent;
        if (!targetLayer) return;

        var spawned = Instantiate(inst.scoreTextPrefab, targetLayer);
        spawned.text = $"+{points}";
        var rt = spawned.rectTransform;

        PlaceAtCellOnLayer(rt, cellRect, targetLayer);

        rt.localScale = Vector3.one * inst.startScale;
        if (inst.setAsLastSibling) rt.SetAsLastSibling();
        spawned.alpha = 0f;

        inst.StartCoroutine(inst.CoPop(spawned));
    }

    static void PlaceAtCellOnLayer(RectTransform spawned, RectTransform cellRect, RectTransform layer)
    {
        if (!spawned || !cellRect || !layer) return;

        Vector3 worldCenter = cellRect.TransformPoint(new Vector3(
            (cellRect.rect.xMin + cellRect.rect.xMax) * 0.5f,
            (cellRect.rect.yMin + cellRect.rect.yMax) * 0.5f,
            0f));

        var canvas = layer.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, cam, out Vector2 local))
        {
            spawned.anchorMin = spawned.anchorMax = new Vector2(0.5f, 0.5f);
            spawned.pivot = cellRect.pivot;
            spawned.anchoredPosition = local;
        }
        else
        {
            spawned.anchorMin = spawned.anchorMax = spawned.pivot = new Vector2(0.5f, 0.5f);
            spawned.anchoredPosition = Vector2.zero;
        }
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

            float fadeIn = Mathf.Min(u * 2f, 1f);
            float fadeOut = Mathf.Max(0f, (u - 0.5f) * 2f);
            t.alpha = Mathf.SmoothStep(0f, 1f, fadeIn) * (1f - Mathf.SmoothStep(0f, 1f, fadeOut));

            yield return null;
        }

        if (t) Destroy(t.gameObject);
    }

    public static int NormalizeRowToBoard(int rawRowBottomOrigin, BoardSpotsNet b)
    {
        return b.AStartsAtTop ? (b.GridRows - 1 - rawRowBottomOrigin) : rawRowBottomOrigin;
    }
}
