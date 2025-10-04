using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class PerimeterEvery5Labeler : MonoBehaviour
{
    [Header("Grid size (auto from Grid constraint if present)")]
    [Min(1)] public int cols = 25;
    [Min(1)] public int rows = 2;

    [Header("Label style")]
    public TMP_FontAsset font;
    public Color labelColor = Color.white;
    public int fontSize = 24;
    [Tooltip("If >0, overrides Font Size with cellHeight * factor (e.g., 0.6).")]
    public float sizeRelativeToCell = 0f;

    [Header("Outline")]
    public bool useOutline = true;
    [Range(0f, 1f)] public float outlineWidth = 0.25f;
    public Color outlineColor = Color.black;

    [Header("Placement")]
    public Vector2 pixelOffset = Vector2.zero;

    [Header("Orientation overrides (after Start Corner / Axis)")]
    public bool flipHorizontally = false;
    public bool flipVertically = false;

    [Header("When to apply")]
    public bool applyOnEnable = true;
    public bool autoInEditMode = true;

    const string kLabelName = "ScoreLabel";

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        var grid = GetComponent<GridLayoutGroup>();
        var parent = (RectTransform)transform;

        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0)
            cols = grid.constraintCount;
        else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && grid.constraintCount > 0)
            rows = grid.constraintCount;

        cols = Mathf.Max(1, cols);
        rows = Mathf.Max(1, rows);

        int expected = cols * rows;
        int childCount = Mathf.Min(parent.childCount, expected);

        float cellH = grid ? grid.cellSize.y : 0f;
        int effFont = (sizeRelativeToCell > 0f && cellH > 0f)
            ? Mathf.RoundToInt(cellH * sizeRelativeToCell)
            : fontSize;

        for (int i = 0; i < childCount; i++)
        {
            IndexToXY(i, cols, rows, grid.startAxis, grid.startCorner,
                      flipHorizontally, flipVertically, out int x, out int y);

            var cell = parent.GetChild(i) as RectTransform;
            if (!cell) continue;

            int idx = PerimeterIndex(x, y, cols, rows);
            string labelText = "";
            if (idx >= 0)
            {
                int oneBased = idx + 1;
                if (oneBased % 5 == 0 && oneBased <= 50)
                    labelText = oneBased.ToString();
            }

            var tmp = GetOrCreateTMP(cell, labelText.Length > 0);
            if (tmp)
            {
                tmp.text = labelText;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                if (font) tmp.font = font;
                tmp.fontSize = effFont;
                tmp.color = labelColor;

                if (useOutline)
                {
                    tmp.outlineWidth = outlineWidth;
                    tmp.outlineColor = outlineColor;
                }
                else
                {
                    tmp.outlineWidth = 0f;
                }

                var tr = (RectTransform)tmp.transform;
                tr.anchorMin = new Vector2(0, 0);
                tr.anchorMax = new Vector2(1, 1);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchoredPosition = pixelOffset;
                tr.sizeDelta = Vector2.zero;
                tr.localScale = Vector3.one;
            }
        }
    }

    static int PerimeterIndex(int x, int y, int cols, int rows)
    {
        int lastCol = cols - 1;
        int lastRow = rows - 1;

        if (y == 0) return x;
        if (x == lastCol) return lastCol + y;
        if (y == lastRow) return lastCol + lastRow + (lastCol - x);
        if (x == 0) return lastCol + lastRow + lastCol + (lastRow - y);
        return -1;
    }

    static void IndexToXY(
        int index, int cols, int rows,
        GridLayoutGroup.Axis axis,
        GridLayoutGroup.Corner corner,
        bool flipH, bool flipV,
        out int x, out int y)
    {
        if (axis == GridLayoutGroup.Axis.Horizontal)
        {
            y = index / cols;
            x = index % cols;
        }
        else
        {
            x = index / rows;
            y = index % rows;
        }

        bool invertXFromCorner = (corner == GridLayoutGroup.Corner.UpperRight || corner == GridLayoutGroup.Corner.LowerRight);
        bool invertYFromCorner = (corner == GridLayoutGroup.Corner.LowerLeft || corner == GridLayoutGroup.Corner.LowerRight);

        bool invertX = invertXFromCorner ^ flipH;
        bool invertY = invertYFromCorner ^ flipV;

        if (invertX) x = cols - 1 - x;
        if (invertY) y = rows - 1 - y;
    }

    TextMeshProUGUI GetOrCreateTMP(RectTransform cell, bool needed)
    {
        Transform existing = cell.Find(kLabelName);
        if (!needed)
        {
#if UNITY_EDITOR
            if (existing) DestroyImmediate(existing.gameObject);
#else
            if (existing) Destroy(existing.gameObject);
#endif
            return null;
        }

        TextMeshProUGUI tmp = null;
        if (!existing)
        {
            var go = new GameObject(kLabelName, typeof(RectTransform), typeof(TextMeshProUGUI));
            var t = go.GetComponent<RectTransform>();
            t.SetParent(cell, false);
            tmp = go.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            tmp = existing.GetComponent<TextMeshProUGUI>();
            if (!tmp) tmp = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }
        return tmp;
    }

    void OnEnable() { if (applyOnEnable) Apply(); }
    void OnValidate() { if (autoInEditMode && !Application.isPlaying) Apply(); }
    void OnTransformChildrenChanged() { if (autoInEditMode && !Application.isPlaying) Apply(); }
}
