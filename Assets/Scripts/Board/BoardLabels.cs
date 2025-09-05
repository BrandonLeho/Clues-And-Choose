using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Adds 1..N numbers to the top/bottom and A.. rows to the left/right edges
/// of a SpriteRenderer-driven board in WORLD SPACE, using TextMeshPro.
/// - Enforces EXACT child counts per side (no duplicates)
/// - Adjustable font, size, offsets, colors, sorting
/// - Auto-repositions when the board moves/rescales
[ExecuteAlways]
public class BoardEdgeLabels2D : MonoBehaviour
{
    [Header("Board")]
    public SpriteRenderer boardRenderer;
    [Tooltip("If assigned, cols/rows are read from this PaletteGrid asset.")]
    public ScriptableObject paletteGrid;   // optional reflection-based read
    [Tooltip("Fallback if PaletteGrid is not provided.")]
    public int cols = 30;
    public int rows = 16;

    [Header("Font & Style")]
    public TMP_FontAsset font;
    public Color numberColor = Color.white;
    public Color letterColor = Color.white;

    [Header("Size")]
    public float fontSize = 36f;
    public float labelScale = 0.1f;
    [Tooltip("If > 0, overrides labelScale using (cell height * value).")]
    public float sizeRelativeToCell = 0.0f;

    [Header("Offsets (cell units if Use Cell Units = true)")]
    public bool useCellUnits = true;
    public Vector2 topOffset = new Vector2(0f, 0.60f);
    public Vector2 bottomOffset = new Vector2(0f, -0.60f);
    public Vector2 leftOffset = new Vector2(-0.60f, 0f);
    public Vector2 rightOffset = new Vector2(0.60f, 0f);

    [Header("Visibility")]
    public bool showTop = true, showBottom = true, showLeft = true, showRight = true;

    [Header("Sorting")]
    public bool matchBoardSortingLayer = true;
    public string sortingLayerName = "Default";
    public int sortingOrderOffset = 10;

    [Header("Advanced")]
    public float extraZ = -0.001f;
    [Tooltip("If true, A is the top row; otherwise A is bottom.")]
    public bool aStartsAtTop = false;

    Transform _root, _top, _bottom, _left, _right;
    readonly List<TextMeshPro> _topLabels = new(), _bottomLabels = new(), _leftLabels = new(), _rightLabels = new();

    void OnEnable()
    {
        if (!boardRenderer) boardRenderer = GetComponent<SpriteRenderer>();
        EnsureParents();
        CacheGridSizeFromPalette();
        RebuildSides();
        UpdateAll();
    }

    void OnValidate()
    {
        if (!boardRenderer) boardRenderer = GetComponent<SpriteRenderer>();
        CacheGridSizeFromPalette();
        EnsureParents();
        RebuildSides();
        UpdateAll();
    }

    void Update()
    {
        UpdateAll();
    }

    void EnsureParents()
    {
        if (!_root)
        {
            var rootGO = GetOrCreateChild(transform, "EdgeLabels");
            _root = rootGO.transform;
        }
        _top = GetOrCreateChild(_root, "Top").transform;
        _bottom = GetOrCreateChild(_root, "Bottom").transform;
        _left = GetOrCreateChild(_root, "Left").transform;
        _right = GetOrCreateChild(_root, "Right").transform;
    }

    GameObject GetOrCreateChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    void CacheGridSizeFromPalette()
    {
        if (!paletteGrid) return;
        var t = paletteGrid.GetType();
        var fCols = t.GetField("cols");
        var fRows = t.GetField("rows");
        var fYTop = t.GetField("yZeroAtTop");
        if (fCols != null) cols = Mathf.Max(1, (int)fCols.GetValue(paletteGrid));
        if (fRows != null) rows = Mathf.Max(1, (int)fRows.GetValue(paletteGrid));
        //if (fYTop != null) aStartsAtTop = (bool)fYTop.GetValue(paletteGrid);
    }

    void RebuildSides()
    {
        if (!boardRenderer || cols <= 0 || rows <= 0) return;

        // enforce exact child count & canonical names for each side
        SyncSide(_topLabels, showTop ? cols : 0, _top, "C");
        SyncSide(_bottomLabels, showBottom ? cols : 0, _bottom, "C");
        SyncSide(_leftLabels, showLeft ? rows : 0, _left, "R");
        SyncSide(_rightLabels, showRight ? rows : 0, _right, "R");
    }

    // --- HARD GUARANTEE: the parent will contain exactly 'count' children named prefix1..prefix<count>.
    void SyncSide(List<TextMeshPro> list, int count, Transform parent, string prefix)
    {
        list.Clear();

        // Desired canonical names
        var desired = new HashSet<string>();
        for (int i = 0; i < count; i++) desired.Add(prefix + (i + 1));

        // 1) Remove any stray children (wrong name or over the limit)
        var toRemove = new List<Transform>();
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (!desired.Contains(c.name))
                toRemove.Add(c);
        }
#if UNITY_EDITOR
        foreach (var c in toRemove) if (c) DestroyImmediate(c.gameObject);
#else
        foreach (var c in toRemove) if (c) Destroy(c.gameObject);
#endif

        // 2) Ensure each canonical child exists and has TMP
        for (int i = 0; i < count; i++)
        {
            string name = prefix + (i + 1);
            var t = parent.Find(name);
            if (!t)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }

            var tmp = t.GetComponent<TextMeshPro>();
            if (!tmp) tmp = t.gameObject.AddComponent<TextMeshPro>();

            // minimal init (full styling happens in UpdateAll)
            tmp.enableAutoSizing = false;
            if (font) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            SetSorting(tmp);

            list.Add(tmp);
        }
    }

    void SetSorting(TextMeshPro tmp)
    {
        var mr = tmp.GetComponent<MeshRenderer>();
        if (!mr) return;
        if (matchBoardSortingLayer && boardRenderer)
        {
            mr.sortingLayerID = boardRenderer.sortingLayerID;
            mr.sortingOrder = boardRenderer.sortingOrder + sortingOrderOffset;
        }
        else
        {
            mr.sortingLayerName = sortingLayerName;
            mr.sortingOrder = sortingOrderOffset;
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

    void UpdateAll()
    {
        if (!boardRenderer || cols <= 0 || rows <= 0) return;

        Bounds b = boardRenderer.bounds;
        float cellW = b.size.x / cols;
        float cellH = b.size.y / rows;

        float worldScale = (sizeRelativeToCell > 0f) ? (cellH * sizeRelativeToCell) : labelScale;

        void Style(TextMeshPro tmp, bool isNumber)
        {
            if (tmp.fontSize != fontSize) tmp.fontSize = fontSize;
            if (font && tmp.font != font) tmp.font = font;
            tmp.color = isNumber ? numberColor : letterColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.transform.localScale = Vector3.one * worldScale;
            SetSorting(tmp);
        }

        Vector2 ToWorld(Vector2 ofsCell) =>
            useCellUnits ? new Vector2(ofsCell.x * cellW, ofsCell.y * cellH) : ofsCell;

        // Columns (1..cols) on Top
        if (_topLabels.Count == cols)
        {
            Vector2 wo = ToWorld(topOffset);
            for (int x = 0; x < cols; x++)
            {
                float cx = Mathf.Lerp(b.min.x, b.max.x, (x + 0.5f) / cols);
                Vector3 pos = new Vector3(cx + wo.x, b.max.y + wo.y, b.center.z + extraZ);
                var tmp = _topLabels[x];
                if (!tmp) continue;
                tmp.text = (x + 1).ToString();
                Style(tmp, true);
                tmp.transform.position = pos;
            }
        }

        // Columns (1..cols) on Bottom
        if (_bottomLabels.Count == cols)
        {
            Vector2 wo = ToWorld(bottomOffset);
            for (int x = 0; x < cols; x++)
            {
                float cx = Mathf.Lerp(b.min.x, b.max.x, (x + 0.5f) / cols);
                Vector3 pos = new Vector3(cx + wo.x, b.min.y + wo.y, b.center.z + extraZ);
                var tmp = _bottomLabels[x];
                if (!tmp) continue;
                tmp.text = (x + 1).ToString();
                Style(tmp, true);
                tmp.transform.position = pos;
            }
        }

        // Rows (A..) on Left
        if (_leftLabels.Count == rows)
        {
            Vector2 wo = ToWorld(leftOffset);
            for (int y = 0; y < rows; y++)
            {
                int letterIndex = aStartsAtTop ? y : (rows - 1 - y);
                float cy = Mathf.Lerp(b.min.y, b.max.y, (y + 0.5f) / rows);
                Vector3 pos = new Vector3(b.min.x + wo.x, cy + wo.y, b.center.z + extraZ);
                var tmp = _leftLabels[y];
                if (!tmp) continue;
                tmp.text = IndexToLetters(letterIndex);
                Style(tmp, false);
                tmp.transform.position = pos;
            }
        }

        // Rows (A..) on Right
        if (_rightLabels.Count == rows)
        {
            Vector2 wo = ToWorld(rightOffset);
            for (int y = 0; y < rows; y++)
            {
                int letterIndex = aStartsAtTop ? y : (rows - 1 - y);
                float cy = Mathf.Lerp(b.min.y, b.max.y, (y + 0.5f) / rows);
                Vector3 pos = new Vector3(b.max.x + wo.x, cy + wo.y, b.center.z + extraZ);
                var tmp = _rightLabels[y];
                if (!tmp) continue;
                tmp.text = IndexToLetters(letterIndex);
                Style(tmp, false);
                tmp.transform.position = pos;
            }
        }
    }
}
