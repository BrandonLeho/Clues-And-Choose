using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CoinArrowSoftMask : MonoBehaviour
{
    [Header("Canvas & Image")]
    public Canvas overlayCanvas;
    public Image overlayImage;

    [Header("Follow / Position")]
    public Camera uiCamera;
    public Vector2 pixelNudge = Vector2.zero;

    [Header("Capsule Shape (UV units)")]
    public Vector2 halfSize = new Vector2(0.18f, 0.10f);
    public float cornerRadius = 0.10f;
    public float feather = 0.05f;

    [Header("Dim Appearance")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.60f);
    [Range(0f, 2f)] public float alphaMultiplier = 1f;

    [Header("Behavior")]
    public bool onlyWhenLocalArrowActive = true;

    Material _matInst;
    RectTransform _rtCanvas;

    void Awake()
    {
        if (!overlayImage)
        {
            GameObject go = new GameObject("SoftCapsuleMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            overlayImage = go.GetComponent<Image>();
            overlayImage.raycastTarget = false;
            var rt = overlayImage.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        if (!overlayCanvas) overlayCanvas = overlayImage.canvas;
        if (!uiCamera && overlayCanvas && overlayCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            uiCamera = overlayCanvas.worldCamera;

        _rtCanvas = overlayCanvas ? overlayCanvas.GetComponent<RectTransform>() : null;

        if (overlayImage.material != null && overlayImage.material.shader != null &&
            overlayImage.material.shader.name.Contains("UI/SoftCapsuleMask"))
        {
            _matInst = new Material(overlayImage.material);
        }
        else
        {
            var shader = Shader.Find("UI/SoftCapsuleMask");
            _matInst = new Material(shader);
        }
        overlayImage.material = _matInst;
    }

    void OnEnable()
    {
        ApplyStaticParams();
    }

    void Update()
    {
        bool shouldShow = true;
        if (onlyWhenLocalArrowActive)
            shouldShow = CoinPlacementProbe.Active != null;

        if (overlayImage.canvasRenderer.GetAlpha() != (shouldShow ? 1f : 0f))
            overlayImage.canvasRenderer.SetAlpha(shouldShow ? 1f : 0f);

        if (!shouldShow) return;

        Vector2 uvCenter = GetProbeCenterUV();
        _matInst.SetVector("_Center", new Vector4(uvCenter.x, uvCenter.y, 0, 0));

        _matInst.SetVector("_HalfSize", new Vector4(halfSize.x, halfSize.y, 0, 0));
        _matInst.SetFloat("_CornerRadius", Mathf.Max(0.0001f, cornerRadius));
        _matInst.SetFloat("_Feather", Mathf.Max(0.0001f, feather));
    }

    void ApplyStaticParams()
    {
        _matInst.SetColor("_Color", dimColor);
        _matInst.SetFloat("_AlphaMult", alphaMultiplier);
        _matInst.SetVector("_HalfSize", new Vector4(halfSize.x, halfSize.y, 0, 0));
        _matInst.SetFloat("_CornerRadius", Mathf.Max(0.0001f, cornerRadius));
        _matInst.SetFloat("_Feather", Mathf.Max(0.0001f, feather));
    }

    Vector2 GetProbeCenterUV()
    {
        Vector2 screenPos;
        if (CoinPlacementProbe.Active != null)
        {
            screenPos = CoinPlacementProbe.Active.GetProbeScreenPosition();
        }
        else
        {
            screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        screenPos += pixelNudge;

        if (overlayCanvas && overlayCanvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera)
        {
            var r = uiCamera.pixelRect;
            return new Vector2(
                Mathf.InverseLerp(r.xMin, r.xMax, screenPos.x),
                Mathf.InverseLerp(r.yMin, r.yMax, screenPos.y)
            );
        }
        else
        {
            return new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
        }
    }
}
