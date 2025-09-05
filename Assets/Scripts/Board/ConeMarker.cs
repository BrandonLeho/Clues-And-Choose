using UnityEngine;
using TMPro;

[ExecuteAlways]
public class ConeMarker2D : MonoBehaviour
{
    [Header("Wiring")]
    public SpriteRenderer body;
    public SpriteRenderer outline;
    public SpriteRenderer shadow;
    public TextMeshPro label;          // optional

    [Header("Size & Fit")]
    [Tooltip("Multiplier vs one board cell size. 0.8 sits nicely inside.")]
    [Range(0.3f, 1.2f)] public float cellFill = 0.8f;
    [Tooltip("Outline expansion relative to body scale.")]
    [Range(1.0f, 1.1f)] public float outlineScale = 1.05f;

    [Header("Shadow")]
    public Vector2 shadowOffset = new(0f, -0.02f);
    [Range(0f, 0.5f)] public float shadowAlpha = 0.18f;
    [Range(0.4f, 1.4f)] public float shadowSize = 0.9f;

    [Header("Colors")]
    public Color bodyColor = Color.magenta;
    [Tooltip("Automatically choose black/white outline for best contrast.")]
    public bool autoContrastOutline = true;
    public Color outlineColorManual = Color.black;

    // Board context (to auto-contrast with the actual square)
    [System.NonSerialized] public BoardGrid2D board;   // assign when spawning
    [System.NonSerialized] public Vector2Int cell;     // assign when spawning

    // --- Public API ---
    public void Configure(Color markerColor, BoardGrid2D grid, Vector2Int cellCoord, string textLabel = null)
    {
        board = grid;
        cell = cellCoord;
        bodyColor = markerColor;

        var center = board.CellCenter(cell);
        transform.position = center;

        FitToCell();
        ApplyVisuals();

        if (label)
        {
            label.text = string.IsNullOrEmpty(textLabel) ? "" : textLabel;
            label.sortingLayerID = body.sortingLayerID;
            label.sortingOrder = body.sortingOrder + 2;
        }
    }

    public void SetColor(Color markerColor)
    {
        bodyColor = markerColor;
        ApplyVisuals();
    }

    public void FitToCell()
    {
        if (!board) return;
        var cs = board.CellSizeWorld();
        var s = Mathf.Min(cs.x, cs.y) * cellFill;
        transform.localScale = Vector3.one * s;

        if (outline) outline.transform.localScale = Vector3.one * s * outlineScale;
        if (shadow)
        {
            shadow.transform.localScale = Vector3.one * s * shadowSize;
            var p = transform.position;
            shadow.transform.position = new Vector3(p.x + shadowOffset.x, p.y + shadowOffset.y, p.z);
        }
    }

    public void ApplyVisuals()
    {
        if (body) body.color = bodyColor;

        if (outline)
        {
            var under = SampleUnderlyingBoardColor();
            Color o = outlineColorManual;

            if (autoContrastOutline)
                o = ChooseBWOutline(bodyColor, under);

            outline.color = o;
        }

        if (shadow)
        {
            var c = shadow.color;
            c.a = shadowAlpha;
            shadow.color = c;
        }
    }

    // --- Contrast helpers ---
    Color SampleUnderlyingBoardColor()
    {
        if (board && board.data)
        {
            // Sample the board’s texture for the exact cell we’re on.
            return board.data.ColorAt(cell.x, cell.y);
        }
        return Color.gray;
    }

    static float RelativeLuminance(Color c)   // WCAG-ish
    {
        float L(float u)
        {
            u = u <= 0.03928f ? u / 12.92f : Mathf.Pow((u + 0.055f) / 1.055f, 2.4f);
            return u;
        }
        float r = L(c.r), g = L(c.g), b = L(c.b);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    static float ContrastRatio(Color a, Color b)
    {
        float La = RelativeLuminance(a) + 0.05f;
        float Lb = RelativeLuminance(b) + 0.05f;
        return La > Lb ? La / Lb : Lb / La;
    }

    static Color ChooseBWOutline(Color body, Color background)
    {
        // Try black and white; pick the one with the best worst-case contrast
        Color black = Color.black, white = Color.white;
        float cBWb = Mathf.Min(ContrastRatio(black, body), ContrastRatio(black, background));
        float cWWb = Mathf.Min(ContrastRatio(white, body), ContrastRatio(white, background));
        return (cBWb >= cWWb) ? black : white;
    }

#if UNITY_EDITOR
    // Keep things tidy in edit mode while tweaking
    void OnValidate()
    {
        ApplyVisuals();
        FitToCell();
    }
#endif
}
