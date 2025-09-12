using UnityEngine;
using UnityEngine.UI;

public class CoinMakerUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Image fill;
    [SerializeField] Image neonRing;
    [SerializeField] Image shadow;

    [Header("Style")]
    [SerializeField] Color playerColor = Color.cyan;
    [SerializeField, Range(0f, 1f)] float faceDarken = 0.15f;
    [SerializeField, Range(0f, 10f)] float glowIntensity = 2.5f;
    [SerializeField, Range(0.02f, 0.3f)] float ringThickness = 0.12f;
    [SerializeField, Range(0f, 0.3f)] float ringSoftness = 0.10f;
    [SerializeField, Range(0f, 1f)] float alpha = 1f;

    [Header("Feedback")]
    [SerializeField] bool pulseOnHover = true;
    [SerializeField, Range(0f, 1f)] float hoverScale = 1.08f;
    [SerializeField, Range(0.1f, 4f)] float pulseSpeed = 1.8f;

    Material _ringMat;
    Vector3 _baseScale;
    bool _hover;

    static readonly int PROP_Color = Shader.PropertyToID("_Color");
    static readonly int PROP_Intensity = Shader.PropertyToID("_Intensity");
    static readonly int PROP_Thickness = Shader.PropertyToID("_Thickness");
    static readonly int PROP_Softness = Shader.PropertyToID("_Softness");
    static readonly int PROP_Alpha = Shader.PropertyToID("_Alpha");

    void Awake()
    {
        _baseScale = transform.localScale;
        EnsureRingMaterial();
        ApplyStyle();
    }

    void Update()
    {
        if (pulseOnHover && _hover && _ringMat)
        {
            if (_ringMat.HasProperty(PROP_Intensity))
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * pulseSpeed);
                _ringMat.SetFloat(PROP_Intensity, glowIntensity * (1f + 0.25f * pulse));
            }
        }
    }

    public void SetPlayerColor(Color c)
    {
        playerColor = c;
        ApplyStyle();
    }

    public void SetHover(bool on)
    {
        _hover = on;
        transform.localScale = on ? _baseScale * hoverScale : _baseScale;
        if (_ringMat && _ringMat.HasProperty(PROP_Intensity))
            _ringMat.SetFloat(PROP_Intensity, glowIntensity);
    }

    void EnsureRingMaterial()
    {
        _ringMat = null;
        if (!neonRing) return;

        var src = neonRing.material;
        if (src != null)
        {
            if (src.HasProperty(PROP_Color) && src.HasProperty(PROP_Intensity))
            {
                _ringMat = new Material(src);
                neonRing.material = _ringMat;
                return;
            }
        }
        neonRing.material = null;
    }

    void ApplyStyle()
    {
        if (fill) fill.color = Color.Lerp(Color.black, playerColor, faceDarken);

        if (_ringMat == null && neonRing && neonRing.material)
        {
            _ringMat = new Material(neonRing.material);
            neonRing.material = _ringMat;
        }

        if (_ringMat)
        {
            var lin = playerColor.linear;

            if (_ringMat.HasProperty("_Color")) _ringMat.SetColor("_Color", lin);
            if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", lin);
            if (_ringMat.HasProperty("_Tint")) _ringMat.SetColor("_Tint", lin);
            if (_ringMat.HasProperty("_EmissionColor"))
            {
                _ringMat.SetColor("_EmissionColor", lin);
                _ringMat.EnableKeyword("_EMISSION");
            }

            if (_ringMat.HasProperty("_Intensity")) _ringMat.SetFloat("_Intensity", glowIntensity);
            if (_ringMat.HasProperty("_Thickness")) _ringMat.SetFloat("_Thickness", ringThickness);
            if (_ringMat.HasProperty("_Softness")) _ringMat.SetFloat("_Softness", ringSoftness);
            if (_ringMat.HasProperty("_Alpha")) _ringMat.SetFloat("_Alpha", alpha);
        }

        if (neonRing)
        {
            neonRing.material = _ringMat;

            var cr = neonRing.canvasRenderer;
            if (cr != null)
            {
                if (cr.materialCount < 1) cr.materialCount = 1;
                cr.SetMaterial(_ringMat, 0);
            }

            neonRing.SetMaterialDirty();
        }

        if (shadow) shadow.color = new Color(0, 0, 0, 0.25f);
    }


#if UNITY_EDITOR
    void OnValidate()
    {
        if (isActiveAndEnabled) ApplyStyle();
    }
#endif


    public void FlashOnPlace(float extraIntensity = 3f, float duration = 0.15f)
    {
        if (!gameObject.activeInHierarchy || _ringMat == null) return;
        if (!_ringMat.HasProperty(PROP_Intensity)) return;

        StartCoroutine(Flash());

        System.Collections.IEnumerator Flash()
        {
            float t = 0f;
            float start = glowIntensity;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = 1f - (t / duration);
                _ringMat.SetFloat(PROP_Intensity, Mathf.Lerp(start + extraIntensity, start, 1f - p * p));
                yield return null;
            }
            _ringMat.SetFloat(PROP_Intensity, start);
        }
    }

    void OnEnable()
    {
        if (neonRing && _ringMat == null && neonRing.material)
        {
            _ringMat = new Material(neonRing.material);
            neonRing.material = _ringMat;
        }
        Canvas.willRenderCanvases += SyncUIMaterial;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= SyncUIMaterial;
    }

    void SyncUIMaterial()
    {
        if (neonRing && _ringMat)
        {
            neonRing.canvasRenderer.SetMaterial(_ringMat, 0);
        }
    }

}
