using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
[DisallowMultipleComponent]
public class HuesCuesScoreboard : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int columns = 25;
    [SerializeField] private int rows = 2; // fixed by spec
    [SerializeField] private float width = 8f;     // world units
    [SerializeField] private float height = 0.8f;  // world units
    [SerializeField] private Vector2 spacing = new Vector2(0.02f, 0.02f);

    [Header("Gradient (clockwise path)")]
    [SerializeField] private Color startGray = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color endGray = new Color(0.90f, 0.90f, 0.90f, 1f);

    [Header("Labels (every 5th)")]
    [SerializeField] private bool showEvery5thLabel = true;
    [SerializeField, Range(1, 50)] private int labelEveryN = 5;
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private int labelFontSize = 24;
    [SerializeField] private Color labelColor = Color.black;
    [SerializeField] private Vector2 labelOffset = new Vector2(0, 0); // local (px in UI units)
    [SerializeField] private Vector2 labelRectSize = new Vector2(60, 40);
    [SerializeField] private TextAlignmentOptions labelAlignment = TextAlignmentOptions.Center;
    [SerializeField] private bool labelsAutoSize = true;
    [SerializeField] private float labelFontMin = 0.1f;
    [SerializeField] private float labelFontMax = 200f;

    [Header("Label Outline")]
    [SerializeField] private bool useOutline = true;
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;
    [SerializeField] private Color outlineColor = Color.black;

    [Header("Canvas")]
    [SerializeField] private int sortingOrder = 5000;

    private const string CanvasName = "Scoreboard_Canvas";
    private const string RootName = "Cells";
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private RectTransform _rootRect;
    private GridLayoutGroup _grid;

    private int TotalCells => columns * rows;

    void Reset()
    {
        columns = 25;
        rows = 2;
        BuildOrUpdate();
    }

    void OnEnable() => BuildOrUpdate();

#if UNITY_EDITOR
    void OnValidate() => BuildOrUpdate();
#endif

    // Public call if you want to refresh from other scripts
    [ContextMenu("Regenerate")]
    public void BuildOrUpdate()
    {
        EnsureCanvasWorldSpace();
        EnsureRootAndGrid();
        ApplyRectSizeAndGrid();
        EnsureExactCellCountAndConfigure();
    }

    private void EnsureCanvasWorldSpace()
    {
        // Find or make a world-space canvas as a child
        Transform existing = transform.Find(CanvasName);
        if (existing == null)
        {
            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = sortingOrder;

            _canvasRect = go.GetComponent<RectTransform>();
            _canvasRect.anchorMin = new Vector2(0.5f, 1f);
            _canvasRect.anchorMax = new Vector2(0.5f, 1f);
            _canvasRect.pivot = new Vector2(0.5f, 1f);
            _canvasRect.sizeDelta = new Vector2(width, height);

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // consistent look in world space
        }
        else
        {
            _canvas = existing.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = sortingOrder;
            _canvasRect = existing as RectTransform;
            _canvasRect.sizeDelta = new Vector2(width, height);
        }
    }

    private void EnsureRootAndGrid()
    {
        Transform existing = _canvasRect.Find(RootName);
        if (existing == null)
        {
            var go = new GameObject(RootName, typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(_canvasRect, false);
            _rootRect = go.GetComponent<RectTransform>();
            _grid = go.GetComponent<GridLayoutGroup>();

            _rootRect.anchorMin = new Vector2(0, 0);
            _rootRect.anchorMax = new Vector2(1, 1);
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
        }
        else
        {
            _rootRect = existing as RectTransform;
            _grid = existing.GetComponent<GridLayoutGroup>();
            if (_grid == null) _grid = existing.gameObject.AddComponent<GridLayoutGroup>();
        }

        _grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.spacing = spacing;
        _grid.childAlignment = TextAnchor.UpperLeft;
    }

    private void ApplyRectSizeAndGrid()
    {
        // Size canvas in world units
        _canvasRect.sizeDelta = new Vector2(width, height);

        // Compute cell size to fit the grid area with spacing
        float totalSpacingX = spacing.x * (columns - 1);
        float totalSpacingY = spacing.y * (rows - 1);

        float cellW = (width - totalSpacingX) / columns;
        float cellH = (height - totalSpacingY) / rows;

        _grid.cellSize = new Vector2(cellW, cellH);
    }

    private void EnsureExactCellCountAndConfigure()
    {
        // Make sure we have exactly TotalCells children named Cell_0..Cell_n
        var keep = new HashSet<string>();
        for (int i = 0; i < TotalCells; i++)
        {
            string name = $"Cell_{i}";
            keep.Add(name);
            Transform child = _rootRect.Find(name);
            if (child == null)
            {
                // Create new
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_rootRect, false);
                child = go.transform;
            }

            // Ensure components
            var rt = child as RectTransform;
            var img = child.GetComponent<Image>();
            img.raycastTarget = false;

            // Color by clockwise path index
            int pathIndex = GetClockwisePathIndex(i); // 0..TotalCells-1
            float t = (TotalCells <= 1) ? 0f : (pathIndex / (float)(TotalCells - 1));
            img.color = Color.Lerp(startGray, endGray, t);

            // Labels on every Nth number
            int labelNumber = GetLabelNumberForCell(i); // 1..50
            bool needsLabel = showEvery5thLabel && (labelNumber % labelEveryN == 0);

            ConfigureLabel(child, needsLabel, labelNumber);
        }

        // Destroy any extra children not in keep, to prevent duplicates forever
        var toRemove = new List<Transform>();
        for (int i = 0; i < _rootRect.childCount; i++)
        {
            Transform ch = _rootRect.GetChild(i);
            if (!keep.Contains(ch.name))
                toRemove.Add(ch);
        }
        foreach (var tr in toRemove)
        {
            SafeDestroy(tr.gameObject);
        }
    }

    private int GetClockwisePathIndex(int flatIndex)
    {
        // flatIndex is row-major: row = i/columns, col = i%columns, top row first, then bottom row
        int row = flatIndex / columns; // 0 or 1
        int col = flatIndex % columns; // 0..24

        if (row == 0)
        {
            // Top row, left->right: 0..24
            return col;
        }
        else
        {
            // Bottom row, RIGHT->LEFT in path: 25..49
            return columns + (columns - 1 - col);
        }
    }

    private int GetLabelNumberForCell(int flatIndex)
    {
        // 1..25 along top row left->right; then 26..50 along bottom row RIGHT->LEFT
        int row = flatIndex / columns;
        int col = flatIndex % columns;

        if (row == 0)
            return col + 1;            // 1..25
        else
            return (columns * rows) - col; // bottom-right=26 (col=24), bottom-left=50 (col=0)
    }

    private void ConfigureLabel(Transform cell, bool needsLabel, int labelNumber)
    {
        const string labelName = "Label";
        Transform labelTr = cell.Find(labelName);

        if (!needsLabel)
        {
            if (labelTr != null) SafeDestroy(labelTr.gameObject);
            return;
        }

        // Ensure label exists (no duplicates — we always reference the same "Label" child name)
        if (labelTr == null)
        {
            var go = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(cell, false);
            labelTr = go.transform;
        }

        var rt = labelTr as RectTransform;
        var tmp = labelTr.GetComponent<TextMeshProUGUI>();

        // Apply style
        if (labelFont != null) tmp.font = labelFont;
        tmp.fontSize = labelFontSize;
        tmp.color = labelColor;
        tmp.alignment = labelAlignment;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = labelsAutoSize;
        tmp.fontSizeMin = labelFontMin;
        tmp.fontSizeMax = labelFontMax;
        tmp.text = labelNumber.ToString();

        // Make sure there's a font; try TMP default if none was given
        if (tmp.font == null)
        {
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
                tmp.font = defaultFont;
        }

        // Apply outline safely (only if we have a font/material that supports it)
        if (useOutline && tmp.font != null)
        {
            // Get current material
            var mat = tmp.fontMaterial;

            if (mat != null)
            {
                // Instantiate once so we don't mutate the shared asset each refresh
                if (!mat.name.EndsWith(" (Instance)"))
                    mat = tmp.fontMaterial = new Material(mat);

                // Only set properties if the shader actually has them
                if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                    tmp.outlineWidth = outlineWidth;

                if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                    tmp.outlineColor = outlineColor;
            }
        }



        // Layout inside cell
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = labelRectSize;
        rt.anchoredPosition = labelOffset;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private void SafeDestroy(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    // Expose setters to change size at runtime (optional)
    public void SetSize(float newWidth, float newHeight)
    {
        width = newWidth;
        height = newHeight;
        BuildOrUpdate();
    }

    public void SetSortingOrder(int order)
    {
        sortingOrder = order;
        if (_canvas != null) _canvas.sortingOrder = order;
    }
}
