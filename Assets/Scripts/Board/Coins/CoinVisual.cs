using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class CoinVisual : MonoBehaviour
{
    [Header("Colors")]
    [ColorUsage(false, true)]
    public Color baseColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 0.5f)] public float ringSaturationBoost = 0.25f;
    [Range(0f, 0.5f)] public float ringValueBoost = 0.25f;
    [Range(0f, 1f)] public float fillDarkenAmount = 0.25f;

    [Header("Shape")]
    [Range(0.2f, 0.49f)] public float radius = 0.40f;
    [Range(0.0f, 0.3f)] public float ringThickness = 0.07f;
    [Range(0.001f, 0.2f)] public float edgeSoftness = 0.02f;
    [Range(0.0f, 0.5f)] public float glowWidth = 0.10f;
    [Range(0.0f, 5.0f)] public float glowBoost = 1.5f;

    [Header("Overrides")]
    public bool forcePureWhite = false;

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

    public void SetForceWhite(bool on)
    {
        forcePureWhite = on;
        Apply();
    }

    public void Apply()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        _sr.GetPropertyBlock(_mpb);

        Color ring, fill;

        if (forcePureWhite)
        {
            ring = Color.white;
            fill = new Color(0.85f, 0.85f, 0.85f, 1f);
        }
        else
        {
            Color.RGBToHSV(baseColor, out var h, out var s, out var v);
            s = Mathf.Clamp01(s + ringSaturationBoost);
            v = Mathf.Clamp01(v + ringValueBoost);
            ring = Color.HSVToRGB(h, s, v);
            ring.a = 1f;

            Color.RGBToHSV(ring, out var fh, out var fs, out var fv);
            fv = Mathf.Lerp(fv, 0f, fillDarkenAmount);
            fill = Color.HSVToRGB(fh, fs, fv);
            fill.a = 1f;
        }

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
