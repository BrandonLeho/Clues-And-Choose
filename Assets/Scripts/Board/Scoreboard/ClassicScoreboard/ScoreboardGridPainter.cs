using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class ScoreboardGridPainter : MonoBehaviour
{
    [Header("Grid size (uses Grid constraint if present)")]
    [Min(1)] public int cols = 25;
    [Min(1)] public int rows = 2;

    [Header("Gray endpoints (top-left = dark → CCW → light)")]
    public Color darkGray = new Color(0.18f, 0.18f, 0.18f);
    public Color lightGray = new Color(0.92f, 0.92f, 0.92f);

    [Header("Orientation overrides (after layout corner/axis)")]
    public bool flipHorizontally = false;
    public bool flipVertically = false;

    [Header("When to apply")]
    public bool applyOnEnable = true;
    public bool autoInEditMode = true;

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
        int count = Mathf.Min(parent.childCount, expected);

        for (int i = 0; i < count; i++)
        {
            IndexToXY(i, cols, rows, grid.startAxis, grid.startCorner,
                      flipHorizontally, flipVertically, out int x, out int y);

            var child = parent.GetChild(i);
            var img = child ? child.GetComponent<Image>() : null;
            if (!img) continue;

            img.color = ColorForPerimeter(x, y, cols, rows, darkGray, lightGray);
        }
    }

    static Color ColorForPerimeter(int x, int y, int cols, int rows, Color darkC, Color lightC)
    {
        int perim = Mathf.Max(1, 2 * cols + 2 * rows - 4);
        int idx;

        if (y == 0)
            idx = x;
        else if (x == cols - 1)
            idx = (cols - 1) + y;
        else if (y == rows - 1)
            idx = (cols - 1) + (rows - 1) + (cols - 1 - x);
        else if (x == 0)
            idx = (cols - 1) + (rows - 1) + (cols - 1) + (rows - 1 - y);
        else
            idx = 0;

        float t = perim > 1 ? idx / (perim - 1f) : 0f;
        return Color.Lerp(darkC, lightC, t);
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

    void OnEnable()
    {
        if (applyOnEnable) Apply();
    }

    void OnValidate()
    {
        if (autoInEditMode && !Application.isPlaying) Apply();
    }

    void OnTransformChildrenChanged()
    {
        if (autoInEditMode && !Application.isPlaying) Apply();
    }
}
