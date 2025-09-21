using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[DisallowMultipleComponent]
public class PerimeterGlowGraphic : MaskableGraphic
{
    [Header("Target")]
    public RectTransform targetGrid;
    public GridLayoutGroup gridLayoutForDims;

    [Header("Palette / Board Sampling")]
    public PaletteGrid paletteGrid;
    public ColorBoard2D colorBoard;

    [Header("Grid Dimensions (fallback)")]
    public int cols = 30;
    public int rows = 16;

    [Header("Ring Shape")]
    [Min(1)] public int segmentsPerCell = 2;
    public float innerInset = -6f;
    public float outerExtent = 28f;

    [Header("Corners")]
    [Range(0f, 128f)] public float cornerRound = 14f;
    [Min(1)] public int cornerSegments = 10;

    [Header("Orientation")]
    public bool flipH = false;
    public bool flipV = false;
    public bool yZeroAtTop = true;

    [Header("Live Update")]
    public bool updateEveryFrame = false;
    public bool trackTargetRectEveryFrame = true;

    [Header("Material (PerimeterGlowURP)")]
    public float intensity = 1.2f;
    public float outerFadePow = 1.5f;
    public float innerFeather = 0.12f;
    public float waves = 0.25f;
    public int waveCount = 6;
    public float waveSpeed = 1.5f;
    public float overallPulse = 0.0f;

    Rect _cachedWorldRect;
    Vector3[] _wc = new Vector3[4];
    int _prevInner = -1, _prevOuter = -1;
    bool _hasPrev = false;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        ApplyMatParams();
        RecalcAndSetVertices();
    }

    void Update()
    {
        if (trackTargetRectEveryFrame && targetGrid)
        {
            targetGrid.GetWorldCorners(_wc);
            var newRect = WorldQuadToLocalRect(_wc);
            if (newRect != _cachedWorldRect)
            {
                _cachedWorldRect = newRect;
                SetVerticesDirty();
            }
        }
        if (updateEveryFrame) SetVerticesDirty();
        ApplyMatParams();
    }

    public void RebuildNow() => RecalcAndSetVertices();

    void ApplyMatParams()
    {
        if (!materialForRendering) return;
        var m = materialForRendering;
        if (m.HasFloat("_Intensity")) m.SetFloat("_Intensity", Mathf.Max(0f, intensity));
        if (m.HasFloat("_OuterFade")) m.SetFloat("_OuterFade", Mathf.Max(0.1f, outerFadePow));
        if (m.HasFloat("_InnerFeather")) m.SetFloat("_InnerFeather", Mathf.Clamp01(innerFeather));
        if (m.HasFloat("_Waves")) m.SetFloat("_Waves", Mathf.Max(0f, waves));
        if (m.HasFloat("_WaveCount")) m.SetFloat("_WaveCount", Mathf.Max(1, waveCount));
        if (m.HasFloat("_WaveSpeed")) m.SetFloat("_WaveSpeed", waveSpeed);
        if (m.HasFloat("_OverallPulse")) m.SetFloat("_OverallPulse", Mathf.Max(0f, overallPulse));
    }

    void RecalcAndSetVertices()
    {
        if (targetGrid)
        {
            targetGrid.GetWorldCorners(_wc);
            _cachedWorldRect = WorldQuadToLocalRect(_wc);
        }
        SetVerticesDirty();
    }

    Rect WorldQuadToLocalRect(Vector3[] wc)
    {
        var tr = rectTransform;
        Vector3 a = tr.InverseTransformPoint(wc[0]);
        Vector3 b = tr.InverseTransformPoint(wc[2]);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        StripReset();

        Rect r = _cachedWorldRect;
        if (r.width <= 1 || r.height <= 1) r = rectTransform.rect;

        int c = InferCols();
        int rows = InferRows();
        if (c < 1 || rows < 1) return;

        float w = r.width + (-2f * innerInset);
        float h = r.height + (-2f * innerInset);

        float cr = Mathf.Max(0f, cornerRound);
        float horizTrim = Mathf.Max(0f, w - 2f * cr);
        float vertTrim = Mathf.Max(0f, h - 2f * cr);
        float arcLen = 0.5f * Mathf.PI * cr;
        float total = 2f * (horizTrim + vertTrim) + 4f * arcLen;
        if (total < 1e-3f) return;

        int samplesTop = Mathf.Max(c * segmentsPerCell, c);
        int samplesBottom = samplesTop;
        int samplesLeft = Mathf.Max(rows * segmentsPerCell, rows);
        int samplesRight = samplesLeft;

        float p = 0f;
        BuildEdge(vh, r, c, rows, Edge.Top, samplesTop, p, horizTrim / total, cr);
        p += horizTrim / total;
        BuildCorner(vh, r, c, rows, Corner.TR, cornerSegments, p, arcLen / total);
        p += arcLen / total;
        BuildEdge(vh, r, c, rows, Edge.Right, samplesRight, p, vertTrim / total, cr);
        p += vertTrim / total;
        BuildCorner(vh, r, c, rows, Corner.BR, cornerSegments, p, arcLen / total);
        p += arcLen / total;
        BuildEdge(vh, r, c, rows, Edge.Bottom, samplesBottom, p, horizTrim / total, cr);
        p += horizTrim / total;
        BuildCorner(vh, r, c, rows, Corner.BL, cornerSegments, p, arcLen / total);
        p += arcLen / total;
        BuildEdge(vh, r, c, rows, Edge.Left, samplesLeft, p, vertTrim / total, cr);
        p += vertTrim / total;
        BuildCorner(vh, r, c, rows, Corner.TL, cornerSegments, p, arcLen / total);
    }

    enum Edge { Top, Right, Bottom, Left }
    enum Corner { TL, TR, BR, BL }

    void BuildEdge(VertexHelper vh, Rect r, int cols, int rows, Edge edge, int samples,
        float progressStart, float progressSpan, float cornerR)
    {
        GetTrimmedEdgeBasis(r, edge, cornerR, out Vector2 innerA, out Vector2 innerB, out Vector2 axis, out Vector2 outward);
        float len = axis.magnitude;
        if (len < 1e-4f) return;
        Vector2 nrm = axis / len;

        for (int i = 0; i <= samples; i++)
        {
            float t = samples == 0 ? 0f : (float)i / samples;
            Vector2 innerPos = innerA + nrm * (t * len);
            float prog = progressStart + t * progressSpan;
            if (prog > 1f) prog -= 1f;

            Color col = SamplePerimeterColor(cols, rows, edge, t);
            StripAppend(vh, innerPos, outward, prog, col);
        }
    }

    void BuildCorner(VertexHelper vh, Rect r, int cols, int rows, Corner corner, int segs,
        float progressStart, float progressSpan)
    {
        if (cornerRound <= 0f || segs < 1) return;

        Vector2 tl = new Vector2(r.xMin - innerInset, r.yMax + innerInset);
        Vector2 tr = new Vector2(r.xMax + innerInset, r.yMax + innerInset);
        Vector2 br = new Vector2(r.xMax + innerInset, r.yMin - innerInset);
        Vector2 bl = new Vector2(r.xMin - innerInset, r.yMin - innerInset);

        Vector2 center; float a0, a1; Edge EA, EB; float tA, tB;
        switch (corner)
        {
            case Corner.TR: center = tr; a0 = 90f; a1 = 0f; EA = Edge.Top; tA = 1f; EB = Edge.Right; tB = 0f; break;
            case Corner.BR: center = br; a0 = 0f; a1 = -90f; EA = Edge.Right; tA = 1f; EB = Edge.Bottom; tB = 0f; break;
            case Corner.BL: center = bl; a0 = -90f; a1 = -180f; EA = Edge.Bottom; tA = 1f; EB = Edge.Left; tB = 0f; break;
            default: center = tl; a0 = 180f; a1 = 90f; EA = Edge.Left; tA = 1f; EB = Edge.Top; tB = 0f; break;
        }

        for (int i = 1; i <= segs; i++)
        {
            float t = (float)i / segs;
            float ang = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, t);
            Vector2 dirOut = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 innerPos = center + dirOut * cornerRound;
            float prog = progressStart + t * progressSpan;
            if (prog > 1f) prog -= 1f;

            Color col = Color.Lerp(
                SamplePerimeterColor(cols, rows, EA, tA),
                SamplePerimeterColor(cols, rows, EB, tB),
                t
            );

            StripAppend(vh, innerPos, dirOut, prog, col);
        }
    }

    void GetTrimmedEdgeBasis(Rect r, Edge edge, float cr,
        out Vector2 innerA, out Vector2 innerB, out Vector2 axis, out Vector2 outward)
    {
        Vector2 tl = new Vector2(r.xMin - innerInset, r.yMax + innerInset);
        Vector2 tr = new Vector2(r.xMax + innerInset, r.yMax + innerInset);
        Vector2 br = new Vector2(r.xMax + innerInset, r.yMin - innerInset);
        Vector2 bl = new Vector2(r.xMin - innerInset, r.yMin - innerInset);

        if (edge == Edge.Top) { innerA = Vector2.Lerp(tl, tr, cr / (tr - tl).magnitude); innerB = Vector2.Lerp(tr, tl, cr / (tr - tl).magnitude); axis = innerB - innerA; outward = Vector2.up; }
        else if (edge == Edge.Right) { innerA = Vector2.Lerp(tr, br, cr / (br - tr).magnitude); innerB = Vector2.Lerp(br, tr, cr / (br - tr).magnitude); axis = innerB - innerA; outward = Vector2.right; }
        else if (edge == Edge.Bottom) { innerA = Vector2.Lerp(br, bl, cr / (bl - br).magnitude); innerB = Vector2.Lerp(bl, br, cr / (bl - br).magnitude); axis = innerB - innerA; outward = Vector2.down; }
        else { innerA = Vector2.Lerp(bl, tl, cr / (tl - bl).magnitude); innerB = Vector2.Lerp(tl, bl, cr / (tl - bl).magnitude); axis = innerB - innerA; outward = Vector2.left; }
    }

    int InferCols() => gridLayoutForDims && gridLayoutForDims.constraint == GridLayoutGroup.Constraint.FixedColumnCount && gridLayoutForDims.constraintCount > 0
        ? gridLayoutForDims.constraintCount : (paletteGrid ? Mathf.Max(1, paletteGrid.cols) : Mathf.Max(1, cols));

    int InferRows() => gridLayoutForDims && gridLayoutForDims.constraint == GridLayoutGroup.Constraint.FixedRowCount && gridLayoutForDims.constraintCount > 0
        ? gridLayoutForDims.constraintCount : (paletteGrid ? Mathf.Max(1, paletteGrid.rows) : Mathf.Max(1, rows));

    Color SamplePerimeterColor(int cols, int rows, Edge edge, float t)
    {
        int x = 0, y = 0;
        switch (edge)
        {
            case Edge.Top: x = Mathf.Clamp(Mathf.RoundToInt(t * (cols - 1)), 0, cols - 1); y = yZeroAtTop ? 0 : rows - 1; break;
            case Edge.Right: x = flipH ? 0 : cols - 1; y = Mathf.Clamp(Mathf.RoundToInt((yZeroAtTop ? t : 1f - t) * (rows - 1)), 0, rows - 1); break;
            case Edge.Bottom: x = Mathf.Clamp(Mathf.RoundToInt((flipH ? t : 1f - t) * (cols - 1)), 0, cols - 1); y = yZeroAtTop ? rows - 1 : 0; break;
            case Edge.Left: x = flipH ? cols - 1 : 0; y = Mathf.Clamp(Mathf.RoundToInt((yZeroAtTop ? 1f - t : t) * (rows - 1)), 0, rows - 1); break;
        }
        if (flipH) x = (cols - 1) - x;
        if (flipV) y = (rows - 1) - y;
        if (paletteGrid) return paletteGrid.ColorAt(x, y);
        if (colorBoard) return colorBoard.ColorAt(x, y);
        return Color.white;
    }

    void StripReset() { _hasPrev = false; _prevInner = _prevOuter = -1; }

    void StripAppend(VertexHelper vh, Vector2 innerPos, Vector2 outwardDir, float prog, Color col)
    {
        innerPos += outwardDir.normalized * 1e-3f;
        int iInner = vh.currentVertCount;
        var v0 = UIVertex.simpleVert; v0.position = innerPos; v0.uv0 = new Vector2(prog, 0f); v0.color = col.linear; vh.AddVert(v0);
        var v1 = UIVertex.simpleVert; v1.position = innerPos + outwardDir * outerExtent; v1.uv0 = new Vector2(prog, 1f); v1.color = col.linear; vh.AddVert(v1);
        if (_hasPrev)
        {
            vh.AddTriangle(_prevInner, iInner, _prevOuter);
            vh.AddTriangle(_prevOuter, iInner, iInner + 1);
        }
        _prevInner = iInner;
        _prevOuter = iInner + 1;
        _hasPrev = true;
    }
}
