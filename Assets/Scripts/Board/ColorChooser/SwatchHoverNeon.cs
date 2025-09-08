using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SwatchHoverNeon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Targets")]
    public RectTransform scaleTarget;           // usually the swatch root

    [Header("Scale")]
    [Range(0.8f, 1.5f)] public float normalScale = 1.00f;
    [Range(0.8f, 1.5f)] public float hoverScale = 1.06f;
    [Range(0.8f, 1.5f)] public float selectedScale = 1.08f; // stays while selected
    [Range(2f, 30f)] public float scaleLerpSpeed = 14f;

    [Header("Pulse passthrough (optional)")]
    // If you also pulse glow via NeonRectBorderBinder on the Glow child,
    // you can leave that script as-is; this class only handles scaling.

    bool _hover;
    bool _selected;
    Vector3 _targetScale;

    void Reset() { scaleTarget = transform as RectTransform; }

    void OnEnable()
    {
        if (!scaleTarget) scaleTarget = transform as RectTransform;
        UpdateTargetScale();                    // honor current _selected/_hover
        scaleTarget.localScale = Vector3.one * normalScale;
    }

    void Update()
    {
        // smooth scale
        float k = 1f - Mathf.Exp(-scaleLerpSpeed * Time.unscaledDeltaTime);
        scaleTarget.localScale = Vector3.Lerp(scaleTarget.localScale, _targetScale, k);
    }

    public void OnPointerEnter(PointerEventData _) { _hover = true; UpdateTargetScale(); }
    public void OnPointerExit(PointerEventData _) { _hover = false; UpdateTargetScale(); }

    public void SetSelected(bool on)
    {
        _selected = on;
        UpdateTargetScale();
    }

    void UpdateTargetScale()
    {
        float s = _selected ? selectedScale : (_hover ? hoverScale : normalScale);
        _targetScale = Vector3.one * s;
    }
}
