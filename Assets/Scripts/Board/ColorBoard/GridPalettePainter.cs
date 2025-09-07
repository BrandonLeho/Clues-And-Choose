using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class GridPalettePainter : MonoBehaviour
{
    [Tooltip("ScriptableObject with ColorAt(x,y).")]
    public PaletteGrid palette;

    [Header("Orientation overrides")]
    [Tooltip("Flip the palette left↔right after layout corner is applied.")]
    public bool flipHorizontally = false;
    [Tooltip("Flip the palette top↔bottom after layout corner is applied.")]
    public bool flipVertically = false;

    [Header("When to apply")]
    public bool applyOnEnable = true;
    public bool autoInEditMode = true;

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (palette == null) return;

        var grid = GetComponent<GridLayoutGroup>();
        var parent = (RectTransform)transform;

        int cols = palette.cols;
        int rows = palette.rows;

        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0)
            cols = grid.constraintCount;
        else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && grid.constraintCount > 0)
            rows = grid.constraintCount;

        int expected = cols * rows;
        int count = Mathf.Min(parent.childCount, expected);

        for (int i = 0; i < count; i++)
        {
            IndexToXY(i, cols, rows, grid.startAxis, grid.startCorner,
                      flipHorizontally, flipVertically, out int x, out int y);

            var child = parent.GetChild(i);
            var img = child ? child.GetComponent<Image>() : null;
            if (!img) continue;

            img.color = palette.ColorAt(x, y);
        }
    }

    // Maps index -> (x,y) respecting GridLayoutGroup fill order,
    // then applies manual flips (horizontal/vertical).
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

        // Corner-implied inversions (Unity layout)
        bool invertXFromCorner = (corner == GridLayoutGroup.Corner.UpperRight || corner == GridLayoutGroup.Corner.LowerRight);
        bool invertYFromCorner = (corner == GridLayoutGroup.Corner.LowerLeft || corner == GridLayoutGroup.Corner.LowerRight);

        // Final inversions = corner ^ manual flip
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
