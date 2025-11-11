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
    [Min(0f)] public float emptyRingDelaySeconds = 0.25f;
    [Min(0f)] public float lastRingLingerSeconds = 1.5f;
    [Min(0)] public int maxRings = 8;

    [Header("Ring Float Fade")]
    public bool ringCellsFadeOnReveal = true;
    [Range(0f, 1f)] public float ringFadeStartAlpha = 0.35f;
    [Min(0f)] public float ringFadeSeconds = 0.18f;
    public AnimationCurve ringFadeEase = null;

    List<GridCellHoverWithCoords> _cells = new List<GridCellHoverWithCoords>();
    GridCellHoverWithCoords[] _indexToCell;
    bool _cacheBuilt;

    Coroutine _co;

    GridCellHoverWithCoords _currentChosen;

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
        }

        t = 0f;
        while (t < ringDelaySeconds) { t += Time.deltaTime; yield return null; }

        for (int ring = 1; ring <= maxRings; ring++)
        {
            bool floatedAny = false;
            bool ringHasAnyCoin = false;

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

                    if (ScorePop.TrySpawnForCell(cc, rr, (RectTransform)cell.transform))
                        ringHasAnyCoin = true;

                    floatedAny = true;
                    ChosenOnTopIfFloating();
                }
            }

            if (!floatedAny)
            {
                float tHold = 0f;
                while (tHold < lastRingLingerSeconds) { tHold += Time.deltaTime; yield return null; }

                if (PhaseController.Instance) PhaseController.Instance.CmdNotifyRingsRevealFinished();
                yield break;
            }

            float delay = ringHasAnyCoin ? ringDelaySeconds : emptyRingDelaySeconds;
            t = 0f;
            while (t < delay) { t += Time.deltaTime; yield return null; }
        }

        float tFinalHold = 0f;
        while (tFinalHold < lastRingLingerSeconds) { tFinalHold += Time.deltaTime; yield return null; }

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

    public void ResetHoverStateFromReveal()
    {
        if (_currentChosen)
        {
            _currentChosen.SetHoverLock(false, keepShown: false);
            _currentChosen.ProbeExit();
            _currentChosen.SetHoverEnabled(false);
            _currentChosen.ForceClearOccupantCoinLift();
        }

        if (_cells != null && _cells.Count > 0)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                if (!c) continue;
                c.ProbeExit();
                c.ForceClearOccupantCoinLift();
            }
        }

        _currentChosen = null;
    }

    public void ReturnAllCellsToOriginalLayerAndOrder()
    {
        if (_cells == null || _cells.Count == 0) return;

        for (int i = 0; i < _cells.Count; i++)
        {
            var c = _cells[i];
            if (!c) continue;

            c.RestoreHomeLayerAndOrder();
            c.SetHoverEnabled(true);
            c.ProbeExit();
            c.ForceClearOccupantCoinLift();
        }

        _currentChosen = null;
    }
}
