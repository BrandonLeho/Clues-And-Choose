using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ConfirmButtonNeonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    public SelectionController picker;
    public NeonRectBorderBinder glow;
    public Button button;
    public RectTransform scaleTarget;

    [Header("Glow States")]
    [Range(0f, 1f)] public float normalAlpha = 0.0f;
    [Range(0f, 1f)] public float hoverAlpha = 1.0f;

    [Range(0f, 10f)] public float normalIntensity = 0.0f;
    [Range(0f, 10f)] public float hoverIntensity = 3.5f;

    [Header("Pulse on Hover")]
    public bool pulseOnHover = true;
    [Range(0f, 1f)] public float hoverPulseAmp = 0.25f;
    [Range(0.05f, 5f)] public float pulseSpeed = 1.2f;

    [Header("Scale on Hover")]
    public bool scaleOnlyWhenInteractable = true;
    [Range(0.8f, 1.5f)] public float normalScale = 1.00f;
    [Range(0.8f, 1.5f)] public float hoverScale = 1.05f;
    [Range(2f, 40f)] public float scaleLerpSpeed = 16f;

    [Header("Animation")]
    public bool useUnscaledTime = true;
    [Range(2f, 40f)] public float glowLerpSpeed = 16f;

    bool _hovering;
    bool _hasColor;
    Color _lastAppliedColor;
    Vector3 _targetScale;

    void Reset()
    {
        button = GetComponent<Button>();
        scaleTarget = transform as RectTransform;
        if (!glow) glow = GetComponentInChildren<NeonRectBorderBinder>(true);
    }

    void Awake()
    {
        if (!button) button = GetComponent<Button>();
        if (!scaleTarget) scaleTarget = transform as RectTransform;

        if (glow)
        {
            var img = glow.GetComponent<Image>();
            if (img) img.raycastTarget = false;

            glow.baseIntensity = normalIntensity;
            glow.alpha = normalAlpha;
            glow.pulse = false;
            glow.Apply();
            glow.gameObject.SetActive(true);
        }

        _targetScale = Vector3.one * normalScale;
        if (scaleTarget) scaleTarget.localScale = _targetScale;

        if (!picker)
        {
#if UNITY_2023_1_OR_NEWER
            picker = Object.FindFirstObjectByType<SelectionController>(FindObjectsInactive.Include);
#else
            picker = Object.FindObjectOfType<SelectionController>(true);
#endif
        }
    }

    void Update()
    {
        if (!button || !picker || !glow) { SmoothScaleOnly(); return; }

        bool buttonVisible = button.gameObject.activeInHierarchy;
        bool canConfirmNow = picker.CanConfirmNow();
        bool hoveringAndLive = _hovering && buttonVisible && button.interactable;

        bool hasSelectable = picker.TryGetCurrentSwatch(out var swatch);
        bool swatchOpen = hasSelectable && swatch != null && !swatch.IsLocked;

        bool canGlow = hoveringAndLive && canConfirmNow && swatchOpen;

        if (canGlow)
        {
            var selColor = swatch.GetFillColor();
            if (!_hasColor || selColor != _lastAppliedColor)
            {
                _lastAppliedColor = selColor;
                _hasColor = true;
                glow.MatchImageColor(selColor);
            }
        }
        else
        {
            _hasColor = false;
        }

        float targetAlpha = canGlow ? hoverAlpha : normalAlpha;
        float targetIntensity = canGlow ? hoverIntensity : normalIntensity;
        float targetPulseAmp = (canGlow && pulseOnHover) ? hoverPulseAmp : 0f;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float kGlow = 1f - Mathf.Exp(-glowLerpSpeed * dt);

        glow.alpha = Mathf.Lerp(glow.alpha, targetAlpha, kGlow);
        glow.baseIntensity = Mathf.Lerp(glow.baseIntensity, targetIntensity, kGlow);
        glow.pulseAmplitude = Mathf.Lerp(glow.pulseAmplitude, targetPulseAmp, kGlow);
        glow.pulse = pulseOnHover;
        glow.pulseSpeed = pulseSpeed;
        glow.Apply();

        bool allowScale = !scaleOnlyWhenInteractable || button.interactable;
        float desired = (_hovering && allowScale) ? hoverScale : normalScale;
        _targetScale = Vector3.one * desired;

        SmoothScaleOnly();
    }

    void SmoothScaleOnly()
    {
        if (!scaleTarget) return;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float kScale = 1f - Mathf.Exp(-scaleLerpSpeed * dt);
        scaleTarget.localScale = Vector3.Lerp(scaleTarget.localScale, _targetScale, kScale);
    }

    public void OnPointerEnter(PointerEventData e) { _hovering = true; }
    public void OnPointerExit(PointerEventData e) { _hovering = false; }
}
