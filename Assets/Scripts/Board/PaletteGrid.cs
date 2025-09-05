using UnityEngine;

[CreateAssetMenu(menuName = "HuesAndCues/AutoPalette (Corner Blend + Pop)")]
public class PaletteGrid : ScriptableObject
{
    [Header("Grid")]
    [Min(1)] public int cols = 30;
    [Min(1)] public int rows = 16;

    [Header("Corner colors (linear RGB 0..1)")]
    public Color topLeft = new Color(115f / 255f, 56f / 255f, 15f / 255f);
    public Color topRight = new Color(122f / 255f, 46f / 255f, 168f / 255f);
    public Color bottomLeft = new Color(217f / 255f, 230f / 255f, 51f / 255f);
    public Color bottomRight = new Color(26f / 255f, 141f / 255f, 224f / 255f);

    [Header("Corner blend shaping (bias midtones)")]
    [Range(0.4f, 2f)] public float gammaX = 1.00f;
    [Range(0.4f, 2f)] public float gammaY = 1.10f;

    [Header("Pastel center shape (tilted ellipse)")]
    [Range(0f, 1f)] public float centerAmount = 0.22f;
    public Vector2 centerOffset = new Vector2(-0.08f, 0.02f);
    [Range(-45f, 45f)] public float centerAngleDeg = -18f;
    public Vector2 centerStretch = new Vector2(1.00f, 1.35f);
    [Range(0.5f, 2.5f)] public float centerGamma = 1.20f;
    [Range(-0.3f, 0.3f)] public float topDarkerBottomLighter = -0.03f;

    [Header("Center whitening (true blend to white)")]
    [Tooltip("How strongly the center mixes toward white in linear light")]
    [Range(0f, 1f)] public float centerWhite = 0.55f;

    [Header("Pop controls")]
    [Range(0f, 1f)] public float saturationBoost = 0.30f;
    [Range(0f, 1f)] public float vibrance = 0.35f;
    [Range(0.8f, 1.5f)] public float valueContrast = 1.12f;
    [Range(-0.2f, 0.2f)] public float valueGain = 0.04f;

    [Header("Orientation")]
    public bool yZeroAtTop = true;

    public Color ColorAt(int x, int y)
    {
        // --- normalize grid
        float nx = (cols <= 1) ? 0f : x / (cols - 1f);
        float ny = (rows <= 1) ? 0f : y / (rows - 1f);

        if (yZeroAtTop) ny = 1f - ny;

        // --- corner blend (RGB) with gamma shaping
        float tx = Mathf.Pow(nx, Mathf.Max(0.0001f, gammaX));
        float ty = Mathf.Pow(ny, Mathf.Max(0.0001f, gammaY));
        Color top = Color.Lerp(topLeft, topRight, tx);
        Color bot = Color.Lerp(bottomLeft, bottomRight, tx);
        Color rgb = Color.Lerp(top, bot, ty);

        // --- center mask (tilted ellipse)
        float x0 = (nx - 0.5f) + centerOffset.x;
        float y0 = (ny - 0.5f) + centerOffset.y;
        float ang = centerAngleDeg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
        float rx = x0 * cs - y0 * sn;
        float ry = x0 * sn + y0 * cs;
        rx /= Mathf.Max(1e-4f, centerStretch.x);
        ry /= Mathf.Max(1e-4f, centerStretch.y);
        float r = Mathf.Clamp01(Mathf.Sqrt(rx * rx + ry * ry) / 0.7071f);
        float mask = Mathf.Pow(1f - r, centerGamma) * centerAmount; // 0 at edges → ~centerAmount at center

        // --- NEW: mix toward white in LINEAR light (true whitening)
        // Convert sRGB->linear
        float rLin = Mathf.GammaToLinearSpace(rgb.r);
        float gLin = Mathf.GammaToLinearSpace(rgb.g);
        float bLin = Mathf.GammaToLinearSpace(rgb.b);
        float wMix = Mathf.Clamp01(mask * centerWhite); // strength of white mix at this cell
        rLin = Mathf.Lerp(rLin, 1f, wMix);
        gLin = Mathf.Lerp(gLin, 1f, wMix);
        bLin = Mathf.Lerp(bLin, 1f, wMix);
        // Back to sRGB
        rgb.r = Mathf.LinearToGammaSpace(rLin);
        rgb.g = Mathf.LinearToGammaSpace(gLin);
        rgb.b = Mathf.LinearToGammaSpace(bLin);

        // --- HSV tweaks for subtle desat/bright + vertical bias
        Color.RGBToHSV(rgb, out float h, out float s, out float v);

        // Keep a bit of the original center behavior (gentle)
        s = Mathf.Lerp(s, s * 0.60f, mask);   // small extra desat toward center
        v = Mathf.Lerp(v, 1f, mask * 0.6f);

        // Vertical tilt (top slightly darker)
        v += Mathf.Lerp(topDarkerBottomLighter, -topDarkerBottomLighter, ny);
        v = Mathf.Clamp01(v);

        // --- Pop controls (suppressed near center so it stays pale)
        float edgeFactor = 1f - mask;                   // 1 at edges, ~0 at center
        float vib = vibrance * edgeFactor;
        float satMul = 1f + saturationBoost * edgeFactor;

        // Vibrance: boost low-sat mid-luminance areas
        float lum = 0.2126f * rgb.r + 0.7152f * rgb.g + 0.0722f * rgb.b;
        float midLum = 1f - Mathf.Abs(2f * lum - 1f);
        float vibFactor = vib * (1f - s) * midLum;
        s = Mathf.Clamp01(s + vibFactor);

        // Uniform saturation push (edge-weighted)
        s = Mathf.Clamp01(s * satMul);

        // Value contrast & lift
        v = Mathf.Clamp01(Mathf.Pow(v, 1f / Mathf.Max(0.0001f, valueContrast)) + valueGain);

        return Color.HSVToRGB(h, s, v);
    }
}
