using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PerimeterGlowFromGrid : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Grid root that has the GridLayoutGroup and the color cells as children.")]
    public RectTransform gridRoot;
    [Tooltip("Animator that fires OnAnimationComplete when the drop-in finishes.")]
    public ColorGridAnimator gridAnimator;
    [Tooltip("Optional: where to place the glow Image. If null, it will be created as a sibling of gridRoot (under gridRoot.parent).")]
    public Transform overlayParent;

    [Header("Glow Look")]
    [Min(1)] public int glowThicknessPx = 28;
    [Min(1)] public int glowSoftnessPx = 36;
    [Range(0.1f, 2f)] public float resolutionScale = 0.5f;
    [Range(0f, 1f)] public float globalAlpha = 0.8f;
    public bool drawUnderGrid = true;

    [Header("Orientation")]
    public bool autoMatchGridStartCorner = true;
    public bool flipHorizontal = false;
    public bool flipVertical = false;

    [Header("Performance")]
    [Tooltip("Clamp texture width/height to this max to avoid huge textures on very large grids.")]
    public int maxTextureSize = 1536;

    [Header("Debug")]
    public bool regenerateInPlayMode = false;

    Image _glowImage;
    Texture2D _glowTex;

    void OnEnable()
    {
        if (gridAnimator) gridAnimator.OnAnimationComplete.AddListener(HandleAnimationComplete);
    }

    void OnDisable()
    {
        if (gridAnimator) gridAnimator.OnAnimationComplete.RemoveListener(HandleAnimationComplete);
    }

    void Update()
    {
        if (regenerateInPlayMode && Application.isPlaying)
        {
            regenerateInPlayMode = false;
            GenerateGlowNow();
        }
    }

    void HandleAnimationComplete() => GenerateGlowNow();

    public void GenerateGlowNow()
    {
        if (!gridRoot) { Debug.LogWarning("[PerimeterGlow] gridRoot not set."); return; }

        var grid = gridRoot.GetComponent<GridLayoutGroup>();
        if (!grid) { Debug.LogWarning("[PerimeterGlow] No GridLayoutGroup on gridRoot."); return; }

        var childImages = new List<Image>(gridRoot.childCount);
        for (int i = 0; i < gridRoot.childCount; i++)
        {
            var img = gridRoot.GetChild(i).GetComponent<Image>();
            if (img) childImages.Add(img);
        }
        if (childImages.Count == 0) { Debug.LogWarning("[PerimeterGlow] No child Images found."); return; }

        var (cols, rows) = GetGridDimensions(grid, childImages.Count);

        bool yZeroIsTop = autoMatchGridStartCorner &&
                          (grid.startCorner == GridLayoutGroup.Corner.UpperLeft ||
                           grid.startCorner == GridLayoutGroup.Corner.UpperRight);

        var topRow = new Color[Mathf.Max(1, cols)];
        var bottomRow = new Color[Mathf.Max(1, cols)];
        var leftCol = new Color[Mathf.Max(1, rows)];
        var rightCol = new Color[Mathf.Max(1, rows)];

        for (int i = 0; i < childImages.Count; i++)
        {
            IndexToXY(i, cols, rows, grid.startAxis, grid.startCorner, out int x, out int y);
            var c = childImages[i].color;

            if (yZeroIsTop)
            {
                if (y == 0) topRow[x] = c;
                if (y == rows - 1) bottomRow[x] = c;
            }
            else
            {
                if (y == 0) bottomRow[x] = c;
                if (y == rows - 1) topRow[x] = c;
            }

            if (x == 0) leftCol[y] = c;
            if (x == cols - 1) rightCol[y] = c;
        }

        var pxSize = ApproximatePixelSize(gridRoot);
        int innerWpx = Mathf.Max(64, Mathf.RoundToInt(((RectTransform)gridRoot).rect.width * pxSize * resolutionScale));
        int innerHpx = Mathf.Max(64, Mathf.RoundToInt(((RectTransform)gridRoot).rect.height * pxSize * resolutionScale));

        float bandPx = glowThicknessPx * resolutionScale;
        float softPx = glowSoftnessPx * resolutionScale;
        int padTex = Mathf.RoundToInt(bandPx + softPx);

        int texW = Mathf.Clamp(innerWpx + 2 * padTex, 64, maxTextureSize);
        int texH = Mathf.Clamp(innerHpx + 2 * padTex, 64, maxTextureSize);

        int innerLeft = padTex;
        int innerRight = texW - 1 - padTex;
        int innerBottom = padTex;
        int innerTop = texH - 1 - padTex;

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[texW * texH];

        for (int y = 0; y < texH; y++)
        {
            float vInner = rows <= 1 ? 0f : Mathf.InverseLerp(innerBottom, innerTop, Mathf.Clamp(y, innerBottom, innerTop));
            int rowIdx = Mathf.Clamp(Mathf.RoundToInt(vInner * (rows - 1)), 0, rows - 1);

            float dOutTop = Mathf.Max(0f, y - innerTop);
            float dOutBottom = Mathf.Max(0f, innerBottom - y);

            for (int x = 0; x < texW; x++)
            {
                float uInner = cols <= 1 ? 0f : Mathf.InverseLerp(innerLeft, innerRight, Mathf.Clamp(x, innerLeft, innerRight));
                int colIdx = Mathf.Clamp(Mathf.RoundToInt(uInner * (cols - 1)), 0, cols - 1);

                float dOutLeft = Mathf.Max(0f, innerLeft - x);
                float dOutRight = Mathf.Max(0f, x - innerRight);

                if (dOutTop == 0f && dOutBottom == 0f && dOutLeft == 0f && dOutRight == 0f)
                {
                    pixels[y * texW + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float aTop = FeatherAlpha(dOutTop, bandPx, softPx);
                float aBottom = FeatherAlpha(dOutBottom, bandPx, softPx);
                float aLeft = FeatherAlpha(dOutLeft, bandPx, softPx);
                float aRight = FeatherAlpha(dOutRight, bandPx, softPx);

                if (aTop <= 0f && aBottom <= 0f && aLeft <= 0f && aRight <= 0f)
                {
                    pixels[y * texW + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Color cTop = topRow[colIdx];
                Color cBottom = bottomRow[colIdx];
                Color cLeft = leftCol[rowIdx];
                Color cRight = rightCol[rowIdx];

                float wTop = dOutTop > 0f ? aTop / (dOutTop + 0.5f) : 0f;
                float wBottom = dOutBottom > 0f ? aBottom / (dOutBottom + 0.5f) : 0f;
                float wLeft = dOutLeft > 0f ? aLeft / (dOutLeft + 0.5f) : 0f;
                float wRight = dOutRight > 0f ? aRight / (dOutRight + 0.5f) : 0f;

                float wSum = wTop + wBottom + wLeft + wRight;
                Color mixed =
                    (wTop > 0 ? cTop * wTop : Color.clear) +
                    (wBottom > 0 ? cBottom * wBottom : Color.clear) +
                    (wLeft > 0 ? cLeft * wLeft : Color.clear) +
                    (wRight > 0 ? cRight * wRight : Color.clear);

                Color outC = wSum > 1e-5f ? mixed / wSum : Color.clear;
                float outA = Mathf.Clamp01((wSum > 1e-5f ? wSum / (wSum + 1f) : 0f)) * globalAlpha;
                outC.a = outA;

                pixels[y * texW + x] = outC;
            }
        }

        FlipPixels(pixels, texW, texH, flipHorizontal, flipVertical);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        int padUI = glowThicknessPx + glowSoftnessPx;
        SetupOrUpdateGlowImageExpanded(tex, padUI);

        _glowTex = tex;
    }

    void SetupOrUpdateGlowImageExpanded(Texture2D tex, int padUI)
    {
        if (!_glowImage)
        {
            var parent = overlayParent ? overlayParent : gridRoot.parent;
            var go = new GameObject("[PerimeterGlow]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gridRoot.gameObject.layer;
            go.transform.SetParent(parent, worldPositionStays: false);

            _glowImage = go.GetComponent<Image>();
            _glowImage.raycastTarget = false;
            _glowImage.maskable = false;
        }

        _glowImage.transform.SetSiblingIndex(drawUnderGrid
            ? gridRoot.GetSiblingIndex()
            : gridRoot.GetSiblingIndex() + 1);

        var rtGlow = (RectTransform)_glowImage.transform;
        rtGlow.anchorMin = gridRoot.anchorMin;
        rtGlow.anchorMax = gridRoot.anchorMax;
        rtGlow.pivot = gridRoot.pivot;
        rtGlow.anchoredPosition = gridRoot.anchoredPosition;

        var baseSize = gridRoot.sizeDelta;
        rtGlow.sizeDelta = new Vector2(baseSize.x + 2 * padUI, baseSize.y + 2 * padUI);

        if (_glowImage.sprite) Destroy(_glowImage.sprite);
        var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        _glowImage.sprite = spr;
        _glowImage.type = Image.Type.Simple;
    }

    static void IndexToXY(int index, int cols, int rows, GridLayoutGroup.Axis axis, GridLayoutGroup.Corner corner, out int x, out int y)
    {
        if (axis == GridLayoutGroup.Axis.Horizontal) { y = index / cols; x = index % cols; }
        else { x = index / rows; y = index % rows; }

        bool invertXFromCorner = (corner == GridLayoutGroup.Corner.UpperRight || corner == GridLayoutGroup.Corner.LowerRight);
        bool invertYFromCorner = (corner == GridLayoutGroup.Corner.LowerLeft || corner == GridLayoutGroup.Corner.LowerRight);

        if (invertXFromCorner) x = cols - 1 - x;
        if (invertYFromCorner) y = rows - 1 - y;
    }

    static (int cols, int rows) GetGridDimensions(GridLayoutGroup grid, int childCount)
    {
        int cols = 1, rows = 1;
        switch (grid.constraint)
        {
            case GridLayoutGroup.Constraint.FixedColumnCount:
                cols = Mathf.Max(1, grid.constraintCount);
                rows = Mathf.Max(1, Mathf.CeilToInt((float)childCount / cols));
                break;
            case GridLayoutGroup.Constraint.FixedRowCount:
                rows = Mathf.Max(1, grid.constraintCount);
                cols = Mathf.Max(1, Mathf.CeilToInt((float)childCount / rows));
                break;
            default:
                var r = (RectTransform)grid.transform;
                var size = r.rect.size - grid.padding.horizontal * Vector2.right - grid.padding.vertical * Vector2.up;
                var cell = grid.cellSize + grid.spacing;
                cols = Mathf.Max(1, Mathf.FloorToInt(size.x / Mathf.Max(1e-3f, cell.x)));
                rows = Mathf.Max(1, Mathf.CeilToInt((float)childCount / cols));
                break;
        }
        return (cols, rows);
    }

    static float FeatherAlpha(float d, float band, float soft)
    {
        if (d <= 0f) return 1f;
        if (d <= band) return 1f;
        float t = Mathf.Clamp01((d - band) / Mathf.Max(1e-3f, soft));
        return 1f - t;
    }

    static float ApproximatePixelSize(RectTransform rt)
    {
        var canvas = rt.GetComponentInParent<Canvas>();
        if (!canvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return 1f;
        return 1f;
    }

    static void FlipPixels(Color32[] pixels, int w, int h, bool flipH, bool flipV)
    {
        if (flipH)
        {
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w / 2; x++)
                {
                    int a = row + x;
                    int b = row + (w - 1 - x);
                    (pixels[a], pixels[b]) = (pixels[b], pixels[a]);
                }
            }
        }

        if (flipV)
        {
            int half = h / 2;
            for (int y = 0; y < half; y++)
            {
                int rowA = y * w;
                int rowB = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    int a = rowA + x;
                    int b = rowB + x;
                    (pixels[a], pixels[b]) = (pixels[b], pixels[a]);
                }
            }
        }
    }
}
