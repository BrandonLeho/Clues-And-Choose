using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class NeonRectBorderBinder : MonoBehaviour
{
    [Header("Glow")]
    public Color glowColor = Color.cyan;
    [Range(0.0f, 10f)] public float baseIntensity = 2.5f;
    [Range(0.0f, 0.5f)] public float thickness = 0.10f;
    [Range(0.0f, 0.5f)] public float softness = 0.10f;
    [Range(0.0f, 0.5f)] public float cornerRadius = 0.08f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Pulse (optional)")]
    public bool pulse = false;
    [Range(0f, 1f)] public float pulseAmplitude = 0.25f;
    [Range(0.05f, 5f)] public float pulseSpeed = 1.3f;
    public bool useUnscaledTime = true;

    RectTransform _rt;
    Image _img;
    Material _mat;

    void Awake()
    {
        _rt = transform as RectTransform;
        _img = GetComponent<Image>();
        _mat = Instantiate(_img.material);
        _img.material = _mat;
        _img.raycastTarget = false; // glow shouldn't block clicks
        Apply();
        UpdateAspect();
    }

    void OnEnable()
    {
        Apply();
        UpdateAspect();
    }

    void OnDestroy()
    {
        if (_mat) Destroy(_mat);
    }

    void Update()
    {
        if (pulse && _mat)
        {
            float t = useUnscaledTime ? Time.unscaledTime : Time.time;
            float k = 1f + pulseAmplitude * Mathf.Sin(t * Mathf.PI * 2f * pulseSpeed);
            _mat.SetFloat("_Intensity", baseIntensity * k);
        }
    }

    void OnRectTransformDimensionsChange() => UpdateAspect();

    void UpdateAspect()
    {
        if (_mat == null || _rt == null) return;
        float w = Mathf.Max(1e-5f, _rt.rect.width);
        float h = Mathf.Max(1e-5f, _rt.rect.height);
        _mat.SetFloat("_RectAspect", w / h);
    }

    public void Apply()
    {
        if (_mat == null) return;
        _mat.SetColor("_Color", glowColor);
        _mat.SetFloat("_Intensity", baseIntensity);
        _mat.SetFloat("_Thickness", thickness);
        _mat.SetFloat("_Softness", softness);
        _mat.SetFloat("_CornerRadius", cornerRadius);
        _mat.SetFloat("_Alpha", alpha);
    }

    // Handy if you want to match the swatch color at runtime
    public void MatchImageColor(Color c)
    {
        glowColor = c;
        if (_mat) _mat.SetColor("_Color", glowColor);
    }
}
