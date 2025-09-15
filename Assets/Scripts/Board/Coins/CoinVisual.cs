using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class CoinVisual : MonoBehaviour
{
    [Header("Colors")]
    [ColorUsage(false, true)]
    public Color baseColor = new Color(0.2f, 0.8f, 0.8f, 1f);

    [Tooltip("Extra saturation added to ring color (HSV).")]
    [Range(0f, 0.5f)] public float ringSaturationBoost = 0.25f;

    [Tooltip("Extra value/brightness added to ring color (HSV).")]
    [Range(0f, 0.5f)] public float ringValueBoost = 0.25f;

    [Tooltip("Darkens the fill relative to the ring. 0 = same as ring, 1 = completely black.")]
    [Range(0f, 1f)] public float fillDarkenAmount = 0.25f;

    [Header("Shape")]
    [Range(0.2f, 0.49f)] public float radius = 0.40f;
    [Range(0.0f, 0.3f)] public float ringThickness = 0.07f;
    [Range(0.001f, 0.2f)] public float edgeSoftness = 0.02f;
    [Range(0.0f, 0.5f)] public float glowWidth = 0.10f;
    [Range(0.0f, 5.0f)] public float glowBoost = 1.5f;

    SpriteRenderer _sr;
    MaterialPropertyBlock _mpb;

    void Reset()
    {
        var sr = GetComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 0;
    }

    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }

    public void SetBaseColor(Color c)
    {
        baseColor = c;
        Apply();
    }

    void Apply()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        _sr.GetPropertyBlock(_mpb);

        Color.RGBToHSV(baseColor, out var h, out var s, out var v);
        s = Mathf.Clamp01(s + ringSaturationBoost);
        v = Mathf.Clamp01(v + ringValueBoost);
        var ring = Color.HSVToRGB(h, s, v);
        ring.a = 1f;

        Color.RGBToHSV(ring, out var fh, out var fs, out var fv);
        fv = Mathf.Lerp(fv, 0f, fillDarkenAmount);
        var fill = Color.HSVToRGB(fh, fs, fv);
        fill.a = 1f;

        _mpb.SetColor("_FillColor", fill);
        _mpb.SetColor("_RingColor", ring);
        _mpb.SetFloat("_Radius", radius);
        _mpb.SetFloat("_RingThick", ringThickness);
        _mpb.SetFloat("_EdgeSoft", edgeSoftness);
        _mpb.SetFloat("_GlowWidth", glowWidth);
        _mpb.SetFloat("_GlowBoost", glowBoost);

        _sr.SetPropertyBlock(_mpb);
    }
}
