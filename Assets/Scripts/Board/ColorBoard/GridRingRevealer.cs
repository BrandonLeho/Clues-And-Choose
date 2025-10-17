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

    List<GridCellHoverWithCoords> _cells = new List<GridCellHoverWithCoords>();
    GridCellHoverWithCoords[] _indexToCell;
    bool _cacheBuilt;

    Coroutine _co;

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

        ApplyExclusiveHover(chosen);

        ForceChosenHoverNow(chosen);

        if (chosen && !chosen.IsFloating) chosen.FloatWithoutHover();

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoRings(chosenCol, chosenRowUI));
    }

    void ForceChosenHoverNow(GridCellHoverWithCoords chosen)
    {
        if (!chosen) return;
        chosen.SetHoverEnabled(true);
        chosen.ProbeEnter();
    }



    IEnumerator CoRings(int c0, int r0)
    {
        float t = 0f;
        while (t < firstRingDelaySeconds) { t += Time.deltaTime; yield return null; }

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

                    if (!cell.IsFloating) cell.FloatWithoutHover();
                    floatedAny = true;
                }
            }

            if (!floatedAny) yield break;

            t = 0f;
            while (t < ringDelaySeconds) { t += Time.deltaTime; yield return null; }
        }
    }

    GridCellHoverWithCoords GetCellComponent(int col, int rowUI)
    {
        if (!_cacheBuilt || _indexToCell == null || _indexToCell.Length == 0) return null;

        int uiRow = rowUI;
        if (flipRowForMapping) uiRow = Mathf.Clamp(rows - 1 - rowUI, 0, rows - 1); // flip here only if caller sends bottom-origin

        int idx = uiRow * cols + col;
        if (idx < 0 || idx >= _indexToCell.Length) return null;

        return _indexToCell[idx];
    }

    void ApplyExclusiveHover(GridCellHoverWithCoords chosen)
    {
        // Grab every GridCellHoverWithCoords in the scene (active or inactive)
        var all = FindObjectsByType<GridCellHoverWithCoords>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            var h = all[i];
            if (!h) continue;

            bool enable = (h == chosen);
            // Only write when needed to avoid extra work
            if (h.enabled != enable) h.SetHoverEnabled(enable);
        }
    }

}
