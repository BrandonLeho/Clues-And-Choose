using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GridRingRevealer : MonoBehaviour
{
    public static GridRingRevealer Instance { get; private set; }

    [Header("Grid Root")]
    [SerializeField] Transform gridRoot;

    [Header("Grid Size")]
    [Min(1)] public int cols = 30;
    [Min(1)] public int rows = 16;

    [Header("Row Mapping")]
    public bool flipRowForMapping = false;

    [Header("Neighborhood")]
    public bool includeDiagonals = true;

    [Header("Timing")]
    [Min(0f)] public float firstRingDelaySeconds = 1.0f;
    [Min(0f)] public float ringDelaySeconds = 1.0f;
    [Min(0)] public int maxRings = 8;

    [Header("Ring Float Fade")]
    public bool ringCellsFadeOnReveal = true;
    [Range(0f, 1f)] public float ringFadeStartAlpha = 0.35f;
    [Min(0f)] public float ringFadeSeconds = 0.18f;
    public AnimationCurve ringFadeEase = null;

    [Header("Clue Giver Bonus Scale")]
    [SerializeField, Range(0.5f, 1f)] float clueGiverTextScale = 0.65f;

    List<GridCellHoverWithCoords> _cells = new List<GridCellHoverWithCoords>();
    GridCellHoverWithCoords[] _indexToCell;
    bool _cacheBuilt;

    Coroutine _co;

    GridCellHoverWithCoords _currentChosen;

    int _chosenColUI, _chosenRowUI;

    void Awake()
    {
        Instance = this;
        if (!gridRoot) gridRoot = transform;
        BuildStableIndexCache();
    }

    void OnEnable()
    {
        if (!_cacheBuilt) BuildStableIndexCache();
    }

    void BuildStableIndexCache()
    {
        _cells.Clear();

        if (!gridRoot)
        {
            return;
        }

        var ordered = new List<GridCellHoverWithCoords>(rows * cols);
        for (int i = 0; i < gridRoot.childCount; i++)
        {
            var c = gridRoot.GetChild(i).GetComponent<GridCellHoverWithCoords>();
            if (c) ordered.Add(c);
        }

        if (ordered.Count != rows * cols)
        {
            Debug.LogWarning(
                $"[GridRingRevealer] Expected {rows * cols} cells under gridRoot, found {ordered.Count}. " +
                $"Check that gridRoot contains the grid cells as direct children."
            );
        }

        _cells.AddRange(ordered);

        int total = Mathf.Max(1, rows * cols);
        _indexToCell = new GridCellHoverWithCoords[total];

        for (int idx = 0; idx < ordered.Count && idx < total; idx++)
            _indexToCell[idx] = ordered[idx];

        _cacheBuilt = true;
    }

    public void Begin(int chosenCol, int chosenRowUI, bool keepOnlyChosenHoverEnabled)
    {
        _chosenColUI = chosenCol;
        _chosenRowUI = chosenRowUI;
        var chosen = GetCellComponent(chosenCol, chosenRowUI);
        _currentChosen = chosen;

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoRings(chosenCol, chosenRowUI, chosen));
    }

    public void ChosenOnTopIfFloating()
    {
        if (_currentChosen && _currentChosen.IsFloating)
        {
            var rt = (RectTransform)_currentChosen.transform;
            rt.SetAsLastSibling();
        }
    }

    IEnumerator CoRings(int c0, int r0, GridCellHoverWithCoords chosen)
    {
        float t = 0f;
        while (t < firstRingDelaySeconds) { t += Time.deltaTime; yield return null; }

        if (chosen)
        {
            chosen.SetHoverLock(true);
            chosen.SetHoverEnabled(true);
            chosen.ProbeEnter();
            ChosenOnTopIfFloating();
            ScorePop.TrySpawnForCell(c0, r0, (RectTransform)chosen.transform);
            StartCoroutine(CoCellSequence(c0, r0, (RectTransform)chosen.transform));
        }

        t = 0f;
        while (t < ringDelaySeconds) { t += Time.deltaTime; yield return null; }

        for (int ring = 1; ring <= maxRings; ring++)
        {
            bool floatedAny = false;

            for (int dc = -ring; dc <= ring; dc++)
            {
                for (int dr = -ring; dr <= ring; dr++)
                {
                    bool onRing = includeDiagonals
                        ? (Mathf.Max(Mathf.Abs(dc), Mathf.Abs(dr)) == ring)
                        : (Mathf.Abs(dc) + Mathf.Abs(dr) == ring);

                    if (!onRing) continue;

                    int cc = c0 + dc;
                    int rr = r0 + dr;
                    if (cc < 0 || cc >= cols || rr < 0 || rr >= rows) continue;

                    var cell = GetCellComponent(cc, rr);
                    if (!cell) continue;

                    if (!cell.IsFloating)
                    {
                        if (ringCellsFadeOnReveal)
                            cell.FloatWithoutHoverFade(ringFadeSeconds, ringFadeEase ?? AnimationCurve.EaseInOut(0, 0, 1, 1), true, ringFadeStartAlpha);
                        else
                            cell.FloatWithoutHover();
                    }
                    ScorePop.TrySpawnForCell(cc, rr, (RectTransform)cell.transform);
                    StartCoroutine(CoCellSequence(cc, rr, (RectTransform)cell.transform));
                    floatedAny = true;

                    ChosenOnTopIfFloating();
                }
            }

            if (!floatedAny)
            {
                if (PhaseController.Instance) PhaseController.Instance.CmdNotifyRingsRevealFinished();
                yield break;
            }

            t = 0f;
            while (t < ringDelaySeconds) { t += Time.deltaTime; yield return null; }
        }

        if (PhaseController.Instance) PhaseController.Instance.CmdNotifyRingsRevealFinished();
    }

    GridCellHoverWithCoords GetCellComponent(int col, int rowUI)
    {
        if (!_cacheBuilt || _indexToCell == null || _indexToCell.Length == 0) return null;

        int uiRow = rowUI;
        if (flipRowForMapping) uiRow = Mathf.Clamp(rows - 1 - rowUI, 0, rows - 1);

        int idx = uiRow * cols + col;
        if (idx < 0 || idx >= _indexToCell.Length) return null;

        return _indexToCell[idx];
    }

    IEnumerator CoCellSequence(int col, int rowUI, RectTransform cellRt)
    {
        yield return new WaitForSeconds(ScorePop.TotalFlightSeconds);

        var phase = PhaseController.Instance;
        if (!phase || !_currentChosen) yield break;

        int dc = Mathf.Abs(col - _chosenColUI);
        int dr = Mathf.Abs(rowUI - _chosenRowUI);
        int cheby = Mathf.Max(dc, dr);

        int vicinity = GetVicinityClient();
        if (cheby >= vicinity) yield break;

        string owner = TryResolveOwnerNameAt(col, rowUI);
        string cg = RosterStore.CurrentClueGiverName;
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(cg) || owner == cg) yield break;

        int perNearby = GetPerNearbyCoinClient();
        if (perNearby <= 0) yield break;

        const float cgScale = 0.85f;
        ScorePop.TrySpawnFromCellToBanner(cg, perNearby, cellRt, cgScale);
    }

    string TryResolveOwnerNameAt(int col, int rowUI)
    {
        var board = BoardSpotsNet.Instance;
        if (!board) return null;

        int rr = flipRowForMapping ? Mathf.Clamp(rows - 1 - rowUI, 0, rows - 1) : rowUI;

        foreach (var kv in board.occupancy)
        {
            if (board.TryGetSpotCoord(kv.Key, out int c, out int r) && c == col && r == rr)
            {
                uint coinNetId = kv.Value;
                if (coinNetId == 0) return null;
                if (!Mirror.NetworkClient.spawned.TryGetValue(coinNetId, out var coinId) || !coinId) return null;

                var coin = coinId.GetComponent<NetworkCoin>();
                if (coin == null || coin.ownerNetId == 0) return null;

                RosterStore.TryGetNameByNetId(coin.ownerNetId, out string name);
                return name;
            }
        }
        return null;
    }

    int GetVicinityClient()
    {
        var phase = PhaseController.Instance;
        if (!phase) return 2;

        var t = phase.GetType();
        var f = t.GetField("vicinitySize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? (int)f.GetValue(phase) : 2;
    }

    int GetPerNearbyCoinClient()
    {
        var phase = PhaseController.Instance;
        if (!phase) return 0;

        int playerCount = (RosterStore.Instance != null && RosterStore.Instance.Names != null)
            ? RosterStore.Instance.Names.Count : 0;

        var t = phase.GetType();
        int fewThr = 3, many = 1, few = 2;
        var fFewThr = t.GetField("fewPlayersThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fMany = t.GetField("pointsPerNearbyCoinManyPlayers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fFew = t.GetField("pointsPerNearbyCoinFewPlayers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fFewThr != null) fewThr = (int)fFewThr.GetValue(phase);
        if (fMany != null) many = (int)fMany.GetValue(phase);
        if (fFew != null) few = (int)fFew.GetValue(phase);

        return (playerCount <= fewThr) ? few : many;
    }
}
