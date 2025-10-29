using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BannerGlowController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform bannerRect;
    [SerializeField] private Image topLightImage;
    [SerializeField] private Image outlineGlowImage;
    [SerializeField] private Image backgroundImage;

    [Header("Shared Color")]
    [ColorUsage(false, true)]
    [SerializeField] private Color playerGlowColor = Color.white;

    [Header("Top Light Settings")]
    [Range(0f, 1f)] public float topHeight = 0.6f;
    [Range(0f, 1f)] public float topFeather = 0.25f;
    [Range(0f, 5f)] public float topIntensity = 1.0f;

    [Header("Outline Glow Settings")]
    [Range(0f, 0.5f)] public float outlineThickness = 0.18f;
    [Range(0f, 1f)] public float outlineFeather = 0.35f;
    [Range(1f, 8f)] public float outlineTopFalloff = 2.0f;
    [Range(0f, 5f)] public float outlineIntensity = 1.0f;

    [Header("Inner UV Source")]
    public bool computeInnerUVFromRects = true;
    public Vector4 manualInnerUV = new Vector4(0.1f, 0.1f, 0.9f, 0.9f);

    Material _topMat;
    Material _outlineMat;
    bool _ownsTop;
    bool _ownsOutline;

    void Awake()
    {
        EnsureInstanceMaterials();
        MakeBackgroundTransparent();
        ApplyAll();
    }

    void OnEnable()
    {
        EnsureInstanceMaterials();
        MakeBackgroundTransparent();
        ApplyAll();
    }

    void OnValidate()
    {
        ApplyAll();
    }

    void OnDestroy()
    {
        ReleaseInstanceMaterials();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && computeInnerUVFromRects)
            ApplyInnerUVFromRects();
#endif
    }

    void EnsureInstanceMaterials()
    {
        if (topLightImage)
        {
            var src = topLightImage.material;
            if (!_ownsTop || _topMat == null || _topMat.shader != (src ? src.shader : null))
            {
                Release(ref _topMat, ref _ownsTop);
                if (src != null)
                {
                    _topMat = topLightImage.material;
                    _ownsTop = true;
                    topLightImage.material = _topMat;
                }
                else
                {
                    _topMat = null;
                    _ownsTop = false;
                }
            }
            else
            {
                _topMat = topLightImage.material;
            }
        }
        else
        {
            Release(ref _topMat, ref _ownsTop);
        }

        if (outlineGlowImage)
        {
            var src = outlineGlowImage.material;
            if (!_ownsOutline || _outlineMat == null || _outlineMat.shader != (src ? src.shader : null))
            {
                Release(ref _outlineMat, ref _ownsOutline);
                if (src != null)
                {
                    _outlineMat = outlineGlowImage.material;
                    _ownsOutline = true;
                    outlineGlowImage.material = _outlineMat;
                }
                else
                {
                    _outlineMat = null;
                    _ownsOutline = false;
                }
            }
            else
            {
                _outlineMat = outlineGlowImage.material;
            }
        }
        else
        {
            Release(ref _outlineMat, ref _ownsOutline);
        }
    }

    void ReleaseInstanceMaterials()
    {
        Release(ref _topMat, ref _ownsTop);
        Release(ref _outlineMat, ref _ownsOutline);
    }

    static void Release(ref Material m, ref bool owned)
    {
        if (owned && m != null)
        {
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
        }
        m = null;
        owned = false;
    }

    void MakeBackgroundTransparent()
    {
        if (!backgroundImage) return;
        var c = backgroundImage.color; c.a = 0f;
        backgroundImage.color = c;
    }

    public void SetPlayerGlowColor(Color c)
    {
        playerGlowColor = c;
        ApplyColor();
    }

    public void ApplyAll()
    {
        ApplyColor();
        ApplyTopParams();
        if (computeInnerUVFromRects) ApplyInnerUVFromRects(); else ApplyInnerUVManual();
        ApplyOutlineParams();
    }

    void ApplyColor()
    {
        if (_topMat != null) _topMat.SetColor("_GlowColor", playerGlowColor);
        if (_outlineMat != null) _outlineMat.SetColor("_GlowColor", playerGlowColor);
    }

    void ApplyTopParams()
    {
        if (_topMat == null) return;
        _topMat.SetFloat("_TopHeight", Mathf.Clamp01(topHeight));
        _topMat.SetFloat("_Feather", Mathf.Clamp01(topFeather));
        _topMat.SetFloat("_Intensity", Mathf.Max(0f, topIntensity));
    }

    void ApplyOutlineParams()
    {
        if (_outlineMat == null) return;
        _outlineMat.SetFloat("_Thickness", Mathf.Clamp01(outlineThickness));
        _outlineMat.SetFloat("_Feather", Mathf.Clamp01(outlineFeather));
        _outlineMat.SetFloat("_TopFalloff", Mathf.Max(1f, outlineTopFalloff));
        _outlineMat.SetFloat("_Intensity", Mathf.Max(0f, outlineIntensity));
    }

    void ApplyInnerUVManual()
    {
        if (_outlineMat == null) return;
        var uv = manualInnerUV;
        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);
        uv.z = Mathf.Clamp01(uv.z);
        uv.w = Mathf.Clamp01(uv.w);
        _outlineMat.SetVector("_InnerUV", uv);
    }

    void ApplyInnerUVFromRects()
    {
        if (_outlineMat == null || outlineGlowImage == null || bannerRect == null) return;

        var outlineRT = outlineGlowImage.rectTransform;

        Vector3[] outlineCorners = new Vector3[4];
        Vector3[] bannerCorners = new Vector3[4];
        outlineRT.GetWorldCorners(outlineCorners);
        bannerRect.GetWorldCorners(bannerCorners);

        var outlineMin = outlineCorners[0];
        var outlineMax = outlineCorners[2];
        var bannerMin = bannerCorners[0];
        var bannerMax = bannerCorners[2];

        float uMin = Mathf.InverseLerp(outlineMin.x, outlineMax.x, bannerMin.x);
        float vMin = Mathf.InverseLerp(outlineMin.y, outlineMax.y, bannerMin.y);
        float uMax = Mathf.InverseLerp(outlineMin.x, outlineMax.x, bannerMax.x);
        float vMax = Mathf.InverseLerp(outlineMin.y, outlineMax.y, bannerMax.y);

        uMin = Mathf.Clamp01(uMin); vMin = Mathf.Clamp01(vMin);
        uMax = Mathf.Clamp01(uMax); vMax = Mathf.Clamp01(vMax);

        _outlineMat.SetVector("_InnerUV", new Vector4(uMin, vMin, uMax, vMax));
    }
}
