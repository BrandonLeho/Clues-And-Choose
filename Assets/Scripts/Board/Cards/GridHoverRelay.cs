using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GridHoverRelay : MonoBehaviour
{
    [Header("Scene Links")]
    public Transform gridRoot;
    public BoardLabels boardLabels;
    public BoardLabelsHighlighter labelsHighlighter;

    [Header("Behavior")]
    public bool simulateGridCellPointer = true;
    public bool incomingRowsBottomOrigin = false;

    public static GridHoverRelay Instance { get; private set; }

    GameObject _lastCellGO;
    int _cols = 1, _rows = 1;

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

        if (labelsHighlighter)
            labelsHighlighter.Highlight(col, rowTopIndex, choiceColor);

        if (simulateGridCellPointer && EventSystem.current)
        {
            int index = rowTopIndex * _cols + col;
            if (index >= 0 && index < gridRoot.childCount)
            {
                var cell = gridRoot.GetChild(index).gameObject;
                _lastCellGO = cell;

                var fake = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(cell, fake, ExecuteEvents.pointerEnterHandler);
            }
        }
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
}