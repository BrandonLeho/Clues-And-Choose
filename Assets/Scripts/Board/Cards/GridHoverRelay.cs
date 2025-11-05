using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class GridHoverRelay : MonoBehaviour
{
    [Header("Scene Links")]
    public Transform gridRoot;
    public BoardLabels boardLabels;
    public BoardLabelsHighlighter labelsHighlighter;

    [Header("Behavior")]
    public bool simulateGridCellPointer = true;
    public bool incomingRowsBottomOrigin = false;

    [Header("Hover Sequencing")]
    public bool waitForPreviousHoverOut = true;
    [Min(0f)] public float previousHoverExitSeconds = 0.20f;
    public bool useUnscaledTime = true;

    public static GridHoverRelay Instance { get; private set; }

    GameObject _lastCellGO;
    int _cols = 1, _rows = 1;

    bool _isSwitching;
    bool _hasQueued;
    int _qCol, _qRow;
    Color _qColor;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
        ResolveGridSize();
    }

    void OnEnable() => ResolveGridSize();

    void ResolveGridSize()
    {
        _cols = 1; _rows = 1;

        if (boardLabels)
        {
            _cols = Mathf.Max(1, boardLabels.cols);
            _rows = Mathf.Max(1, boardLabels.rows);
        }
        else if (gridRoot)
        {
            var glg = gridRoot.GetComponent<GridLayoutGroup>();
            if (glg)
            {
                if (glg.constraint == GridLayoutGroup.Constraint.FixedColumnCount && glg.constraintCount > 0)
                    _cols = glg.constraintCount;
                else if (glg.constraint == GridLayoutGroup.Constraint.FixedRowCount && glg.constraintCount > 0)
                    _rows = glg.constraintCount;
                else
                {
                    int count = gridRoot.childCount;
                    _cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(count)));
                    _rows = Mathf.Max(1, Mathf.CeilToInt((float)count / _cols));
                }
            }
        }
    }

    public void HoverEnter(int col, int row, Color choiceColor)
    {
        if (!gridRoot) return;
        ResolveGridSize();

        int rowTopIndex = incomingRowsBottomOrigin ? (_rows - 1 - row) : row;
        col = Mathf.Clamp(col, 0, _cols - 1);
        rowTopIndex = Mathf.Clamp(rowTopIndex, 0, _rows - 1);

        if (!waitForPreviousHoverOut)
        {
            DoImmediateEnter(col, rowTopIndex, choiceColor);
            return;
        }

        _qCol = col;
        _qRow = rowTopIndex;
        _qColor = choiceColor;
        _hasQueued = true;

        if (!_isSwitching)
            StartCoroutine(Co_SwitchLoop());
    }

    public void HoverExit()
    {
        if (labelsHighlighter)
            labelsHighlighter.Clear();

        if (simulateGridCellPointer && EventSystem.current && _lastCellGO)
        {
            var fake = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(_lastCellGO, fake, ExecuteEvents.pointerExitHandler);
            _lastCellGO = null;
        }
    }

    void DoImmediateEnter(int col, int rowTopIndex, Color choiceColor)
    {
        if (labelsHighlighter)
            labelsHighlighter.Highlight(col, rowTopIndex, choiceColor);

        if (!simulateGridCellPointer || !EventSystem.current) return;

        int index = rowTopIndex * _cols + col;
        if (index < 0 || index >= gridRoot.childCount) return;

        var cell = gridRoot.GetChild(index).gameObject;
        _lastCellGO = cell;

        var fake = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(cell, fake, ExecuteEvents.pointerEnterHandler);
    }

    IEnumerator Co_SwitchLoop()
    {
        _isSwitching = true;

        while (_hasQueued)
        {
            int targetCol = _qCol;
            int targetRow = _qRow;
            Color targetColor = _qColor;
            _hasQueued = false;

            if (simulateGridCellPointer && EventSystem.current && _lastCellGO)
            {
                var fake = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(_lastCellGO, fake, ExecuteEvents.pointerExitHandler);
                _lastCellGO = null;
            }

            if (labelsHighlighter)
                labelsHighlighter.Clear();

            float t = 0f;
            float dt()
                => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            while (t < previousHoverExitSeconds)
            {
                t += dt();
                yield return null;
            }

            if (_hasQueued)
            {
                continue;
            }

            if (labelsHighlighter)
                labelsHighlighter.Highlight(targetCol, targetRow, targetColor);

            if (simulateGridCellPointer && EventSystem.current)
            {
                int index = targetRow * _cols + targetCol;
                if (index >= 0 && index < gridRoot.childCount)
                {
                    var cell = gridRoot.GetChild(index).gameObject;
                    _lastCellGO = cell;

                    var fake = new PointerEventData(EventSystem.current);
                    ExecuteEvents.Execute(cell, fake, ExecuteEvents.pointerEnterHandler);
                }
            }

            yield return null;
        }

        _isSwitching = false;
    }
}
