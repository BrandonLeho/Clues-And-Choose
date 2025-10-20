using UnityEngine;
using System.Collections.Generic;
using TMPro;

public sealed class ScoreFloatPopper : MonoBehaviour
{
    public static ScoreFloatPopper Instance { get; private set; }

    [Header("UI")]
    [SerializeField] Canvas popupCanvas;
    [SerializeField] ScoreFloatText popupPrefab;
    [SerializeField] TMP_FontAsset numberFont;
    [SerializeField, Min(0)] int pointsAtExactDefault = 3;

    Camera _uiCam;

    void OnEnable()
    {
        GridRingRevealer.OnCellRevealed += HandleCellRevealed;
    }
    void OnDisable()
    {
        GridRingRevealer.OnCellRevealed -= HandleCellRevealed;
    }
    void HandleCellRevealed(int col, int row, int ring, Transform t)
    {
        Instance.SpawnForCell(col, row, ring, t);
    }


    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!popupCanvas) popupCanvas = FindFirstObjectByType<Canvas>();
        _uiCam = Camera.main;
    }

    public void SpawnForCell(int col, int row, int ringIndex, Transform cellTransform)
    {
        int pointsAtExact = PhaseController.Instance ? GetPrivatePointsAtExact() : pointsAtExactDefault;
        int points = Mathf.Max(0, pointsAtExact - ringIndex);
        if (points <= 0) return;

        if (!TryGetCoinOwnerAt(col, row, out string ownerName, out RectTransform bannerScoreAnchor))
            return;

        if (!bannerScoreAnchor) return;

        Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(_uiCam, cellTransform.position);
        var fx = Instantiate(popupPrefab, popupCanvas.transform);
        fx.Init(startScreen, bannerScoreAnchor, points, numberFont);
    }

    bool TryGetCoinOwnerAt(int col, int row, out string ownerName, out RectTransform bannerScoreAnchor)
    {
        ownerName = null;
        bannerScoreAnchor = null;

        var board = BoardSpotsNet.Instance;
        if (!board) return false;

        if (!board.TryGetSpotIndexAt(col, row, out int spotIndex)) return false;

        if (!board.occupancy.ContainsKey(spotIndex)) return false;
        uint coinNetId = board.occupancy[spotIndex];
        if (coinNetId == 0) return false;

        if (!Mirror.NetworkClient.spawned.TryGetValue(coinNetId, out var id) || !id)
            return false;

        var coin = id.GetComponent<NetworkCoin>();
        if (!coin || coin.ownerNetId == 0) return false;

        if (!RosterStore.TryGetNameByNetId(coin.ownerNetId, out ownerName) || string.IsNullOrWhiteSpace(ownerName))
            return false;

        bannerScoreAnchor = ScoreBannerEntry.TryGetScoreAnchor(ownerName);
        return bannerScoreAnchor;
    }

    int GetPrivatePointsAtExact()
    {
        return pointsAtExactDefault;
    }
}
