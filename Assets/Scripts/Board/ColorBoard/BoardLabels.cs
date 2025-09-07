using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class BoardLabels : MonoBehaviour
{
    [Header("Grid")]
    public GridLayoutGroup grid;
    public ScriptableObject paletteGrid;
    public int cols = 30;
    public int rows = 16;

    [Header("Font & Style")]
    public TMP_FontAsset font;
    public Color numberColor = Color.white;
    public Color letterColor = Color.white;
    public int fontSize = 24;
    [Tooltip("If > 0, overrides Font Size with (cellHeight * factor). e.g. 0.5")]
    public float sizeRelativeToCell = 0f;

    [Header("Offsets (cell units if enabled)")]
    public bool useCellUnits = true;
    public Vector2 topOffset = new Vector2(0f, 0.60f);
    public Vector2 bottomOffset = new Vector2(0f, -0.60f);
    public Vector2 leftOffset = new Vector2(-0.60f, 0f);
    public Vector2 rightOffset = new Vector2(0.60f, 0f);

    [Header("Visibility")]
    public bool showTop = true, showBottom = true, showLeft = true, showRight = true;

    [Header("Advanced")]
    [Tooltip("If true, 'A' is the top row; otherwise 'A' is bottom.")]
    public bool aStartsAtTop = false;
    [Tooltip("Move this object to the end of its Canvas hierarchy so labels render on top.")]
    public bool bringToFront = true;

    RectTransform _rt, _root, _top, _bottom, _left, _right;
    readonly List<TextMeshProUGUI> _topLabels = new();
    readonly List<TextMeshProUGUI> _bottomLabels = new();
    readonly List<TextMeshProUGUI> _leftLabels = new();
    readonly List<TextMeshProUGUI> _rightLabels = new();

    bool _dirty;
    bool _validating;

    void MarkDirty() { _dirty = true; }

    void OnEnable()
    {
        if (!grid) grid = GetComponent<GridLayoutGroup>();
        _rt = GetComponent<RectTransform>();
        MarkDirty();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!grid) grid = GetComponent<GridLayoutGroup>();
        _rt = GetComponent<RectTransform>();
        CacheGridSizeFromSources();

        _validating = true;
        MarkDirty();

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            _validating = false;
            MarkDirty();
        };
    }
#endif

    void OnRectTransformDimensionsChange()
    {
        if (_validating) return;
        MarkDirty();
    }

    void OnTransformChildrenChanged() => MarkDirty();

    void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;

        if (!grid) grid = GetComponent<GridLayoutGroup>();
        if (!_rt) _rt = GetComponent<RectTransform>();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);

        EnsureParents();
        CacheGridSizeFromSources();
        RebuildSides();
        UpdateAll();
        BringToFrontIfNeeded();
    }

    void EnsureParents()
    {
        _root = GetOrCreateRect((RectTransform)transform, "EdgeLabelsUI", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        _top = GetOrCreateRect(_root, "Top", new Vector2(0.5f, 0.5f));
        _bottom = GetOrCreateRect(_root, "Bottom", new Vector2(0.5f, 0.5f));
        _left = GetOrCreateRect(_root, "Left", new Vector2(0.5f, 0.5f));
        _right = GetOrCreateRect(_root, "Right", new Vector2(0.5f, 0.5f));
    }

    static RectTransform GetOrCreateRect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var t = parent.Find(name) as RectTransform;
        if (!t)
        {
            var go = new GameObject(name, typeof(RectTransform));
            t = go.GetComponent<RectTransform>();
            t.SetParent(parent, false);
        }
        t.anchorMin = anchorMin; t.anchorMax = anchorMax; t.pivot = pivot;
        t.anchoredPosition = Vector2.zero; t.sizeDelta = Vector2.zero; t.localScale = Vector3.one;
        return t;
    }
    static RectTransform GetOrCreateRect(RectTransform parent, string name, Vector2 pivot)
        => GetOrCreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pivot);

    void BringToFrontIfNeeded()
    {
        if (bringToFront && _rt) _rt.SetAsLastSibling();
    }

    void CacheGridSizeFromSources()
    {
        if (grid)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0)
                cols = grid.constraintCount;
            else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && grid.constraintCount > 0)
                rows = grid.constraintCount;
        }

        if (paletteGrid)
        {
            var t = paletteGrid.GetType();
            var fCols = t.GetField("cols");
            var fRows = t.GetField("rows");
            if (fCols != null) cols = Mathf.Max(1, (int)fCols.GetValue(paletteGrid));
            if (fRows != null) rows = Mathf.Max(1, (int)fRows.GetValue(paletteGrid));
        }

        cols = Mathf.Max(1, cols);
        rows = Mathf.Max(1, rows);
    }

    void RebuildSides()
    {
        if (!_rt || cols <= 0 || rows <= 0) return;
        SyncSide(_topLabels, showTop ? cols : 0, _top, "C");
        SyncSide(_bottomLabels, showBottom ? cols : 0, _bottom, "C");
        SyncSide(_leftLabels, showLeft ? rows : 0, _left, "R");
        SyncSide(_rightLabels, showRight ? rows : 0, _right, "R");
    }

    void SyncSide(List<TextMeshProUGUI> list, int count, RectTransform parent, string prefix)
    {
        if (!parent) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var ch = parent.GetChild(i);
            bool keep = false;
            if (prefix == "C") keep = i < count;
            if (prefix == "R") keep = i < count;
            if (!keep)
            {
#if UNITY_EDITOR
                DestroyImmediate(ch.gameObject);
#else
                Destroy(ch.gameObject);
#endif
            }
        }

        while (parent.childCount < count)
        {
            var go = new GameObject(prefix + (parent.childCount + 1), typeof(RectTransform), typeof(TextMeshProUGUI));
            var t = go.GetComponent<RectTransform>();
            t.SetParent(parent, false);
        }

        list.Clear();
        for (int i = 0; i < count; i++)
        {
            var t = (RectTransform)parent.GetChild(i);
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (!tmp) tmp = t.gameObject.AddComponent<TextMeshProUGUI>();
            list.Add(tmp);
        }
    }

    void UpdateAll()
    {
        if (!_rt || cols <= 0 || rows <= 0) return;

        var rect = _rt.rect;
        float w = rect.width;
        float h = rect.height;

        var pad = grid ? grid.padding : new RectOffset();
        var spacing = grid ? grid.spacing : Vector2.zero;

        float cellW = grid ? grid.cellSize.x : (w / cols);
        float cellH = grid ? grid.cellSize.y : (h / rows);

        float firstCenterX = -w * 0.5f + pad.left + cellW * 0.5f;
        float firstCenterY = h * 0.5f - pad.top - cellH * 0.5f;

        int effectiveFontSize = fontSize;
        if (sizeRelativeToCell > 0f) effectiveFontSize = Mathf.RoundToInt(cellH * sizeRelativeToCell);

        Vector2 ToUI(Vector2 ofs) => useCellUnits ? new Vector2(ofs.x * cellW, ofs.y * cellH) : ofs;

        void StyleTMP(TextMeshProUGUI tmp, bool isNumber)
        {
            if (!tmp) return;
            if (font && tmp.font != font) tmp.font = font;
            if (tmp.fontSize != effectiveFontSize) tmp.fontSize = effectiveFontSize;

            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmp.color = isNumber ? numberColor : letterColor;

            var tr = (RectTransform)tmp.transform;
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = Vector2.zero;
            tr.localScale = Vector3.one;
        }

        if (_topLabels.Count == cols)
        {
            Vector2 o = ToUI(topOffset);
            for (int x = 0; x < cols; x++)
            {
                float px = firstCenterX + x * (cellW + spacing.x) + o.x;
                float py = h * 0.5f + o.y;
                var tmp = _topLabels[x];
                if (!tmp) continue;
                tmp.text = (x + 1).ToString();
                StyleTMP(tmp, true);
                ((RectTransform)tmp.transform).anchoredPosition = new Vector2(px, py);
            }
        }

        if (_bottomLabels.Count == cols)
        {
            Vector2 o = ToUI(bottomOffset);
            for (int x = 0; x < cols; x++)
            {
                float px = firstCenterX + x * (cellW + spacing.x) + o.x;
                float py = -h * 0.5f + o.y;
                var tmp = _bottomLabels[x];
                if (!tmp) continue;
                tmp.text = (x + 1).ToString();
                StyleTMP(tmp, true);
                ((RectTransform)tmp.transform).anchoredPosition = new Vector2(px, py);
            }
        }

        if (_leftLabels.Count == rows || _rightLabels.Count == rows)
        {
            Vector2 oL = ToUI(leftOffset);
            Vector2 oR = ToUI(rightOffset);

            for (int y = 0; y < rows; y++)
            {
                int letterIndex = aStartsAtTop ? y : (rows - 1 - y);
                float cy = firstCenterY - y * (cellH + spacing.y);

                if (_leftLabels.Count == rows)
                {
                    var tmp = _leftLabels[y];
                    if (tmp)
                    {
                        tmp.text = IndexToLetters(letterIndex);
                        StyleTMP(tmp, false);
                        ((RectTransform)tmp.transform).anchoredPosition = new Vector2(-w * 0.5f + oL.x, cy + oL.y);
                    }
                }

                if (_rightLabels.Count == rows)
                {
                    var tmp = _rightLabels[y];
                    if (tmp)
                    {
                        tmp.text = IndexToLetters(letterIndex);
                        StyleTMP(tmp, false);
                        ((RectTransform)tmp.transform).anchoredPosition = new Vector2(w * 0.5f + oR.x, cy + oR.y);
                    }
                }
            }
        }
    }

    static string IndexToLetters(int idx)
    {
        idx = Mathf.Max(0, idx);
        string s = "";
        while (idx >= 0)
        {
            int rem = idx % 26;
            s = (char)('A' + rem) + s;
            idx = idx / 26 - 1;
        }
        return s;
    }
}
