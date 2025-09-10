using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

[RequireComponent(typeof(Button))]
public class ColorSwatch : MonoBehaviour, IPointerClickHandler
{
    [Header("Refs")]
    public Image fillImage;
    public SwatchHoverNeon hover;
    public RectTransform scaleTarget;
    public Image lockOverlay;

    [Header("Lock Overlay FX")]
    public bool fadeOverlay = true;
    [Range(0.01f, 0.6f)] public float fadeDuration = 0.18f;
    public bool overlayBringToFront = true;
    [Range(0f, 1f)] public float lockOverlayAlpha = 1f;

    [Header("Lock-In Anim (banner)")]
    [Tooltip("Duration for the banner to ease back to normal scale after locking.")]
    [Range(0.05f, 0.6f)] public float lockScaleDuration = 0.18f;
    public AnimationCurve lockScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Lock Icon Pop")]
    [Tooltip("Icon spawns bigger, then eases down to its set size.")]
    public bool iconPopIn = true;
    [Range(1.0f, 2.0f)] public float iconPopScale = 1.25f;
    [Range(0.05f, 0.6f)] public float iconPopDuration = 0.18f;
    public AnimationCurve iconPopCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Owner Label UI")]
    [SerializeField] TextMeshProUGUI ownerText;
    [SerializeField] RectTransform ownerRect;
    [SerializeField] CanvasGroup ownerGroup;

    [Header("Owner Label Anim")]
    [SerializeField] float slideDuration = 0.25f;
    [SerializeField] float hiddenYOffset = -40f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Clipping")]
    [SerializeField] RectTransform clipArea;

    [Header("State (read-only)")]
    public bool IsSelected { get; private set; }
    public bool IsLocked { get; private set; }

    internal SelectionController owner;


    Button _btn;
    Coroutine _fadeCo;
    Coroutine _scaleCo;
    Coroutine _iconPopCo;
    Coroutine _ownerCo;
    RectTransform _iconRT;

    string _ownerShownText;
    bool _ownerVisible;

    void Awake()
    {
        if (ownerText && !ownerRect) ownerRect = ownerText.rectTransform;
        if (ownerText && !ownerGroup) ownerGroup = ownerText.GetComponent<CanvasGroup>();
        if (ownerText && ownerRect && ownerGroup) ResetOwnerLabelDefaults();

        if (!clipArea) clipArea = GetComponent<RectTransform>();
        if (clipArea && !clipArea.GetComponent<RectMask2D>())
            clipArea.gameObject.AddComponent<RectMask2D>();

        _btn = GetComponent<Button>();
        if (!scaleTarget) scaleTarget = transform as RectTransform;
        _iconRT = lockOverlay ? lockOverlay.rectTransform : null;

        if (lockOverlay)
        {
            lockOverlay.raycastTarget = false;
            if (overlayBringToFront) lockOverlay.transform.SetAsLastSibling();
            SetOverlayAlpha(0f);
            lockOverlay.enabled = false;
        }

        ResetOwnerLabelDefaults();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (IsLocked) return;
        owner?.Select(this);
    }

    internal void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (hover)
        {
            hover.SetSelected(selected);
            hover.enabled = !IsLocked;
        }
    }

    internal void Lock()
    {
        if (IsLocked) return;
        IsLocked = true;

        if (_btn) _btn.interactable = false;
        if (hover) hover.enabled = false;

        float normal = (hover != null) ? hover.normalScale : 1f;
        StartScaleTo(Vector3.one * normal, lockScaleDuration, lockScaleCurve);

        SetLockOverlayVisible(true);
    }

    internal void Unlock()
    {
        if (!IsLocked) return;
        IsLocked = false;

        if (_btn) _btn.interactable = true;
        if (hover) hover.enabled = true;

        SetLockOverlayVisible(false);
    }

    public Color GetFillColor() => fillImage ? fillImage.color : Color.white;

    void StartScaleTo(Vector3 target, float dur, AnimationCurve curve)
    {
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        _scaleCo = StartCoroutine(ScaleToCo(target, dur, curve));
    }

    IEnumerator ScaleToCo(Vector3 target, float dur, AnimationCurve curve)
    {
        if (!scaleTarget || dur <= 0f)
        {
            if (scaleTarget) scaleTarget.localScale = target;
            yield break;
        }

        Vector3 start = scaleTarget.localScale;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float k = curve != null ? curve.Evaluate(p) : p;
            scaleTarget.localScale = Vector3.LerpUnclamped(start, target, k);
            yield return null;
        }
        scaleTarget.localScale = target;
        _scaleCo = null;
    }

    void SetLockOverlayVisible(bool on)
    {
        if (!lockOverlay) return;

        lockOverlay.enabled = true;
        if (_iconPopCo != null) { StopCoroutine(_iconPopCo); _iconPopCo = null; }

        float targetA = on ? Mathf.Clamp01(lockOverlayAlpha) : 0f;

        if (on && iconPopIn && _iconRT)
        {
            if (isActiveAndEnabled)
            {
                _iconRT.localScale = Vector3.one * iconPopScale;
                _iconPopCo = StartCoroutine(IconPopInCo(iconPopDuration, iconPopCurve));
            }
            else
            {
                _iconRT.localScale = Vector3.one;
            }
        }

        if (fadeOverlay)
        {
            SafeStart(ref _fadeCo, FadeOverlay(targetA, fadeDuration, on), () =>
            {
                SetOverlayAlpha(targetA);
                if (!on) lockOverlay.enabled = false;
            });
        }
        else
        {
            SetOverlayAlpha(targetA);
            if (!on) lockOverlay.enabled = false;
        }
    }


    IEnumerator IconPopInCo(float dur, AnimationCurve curve)
    {
        if (!_iconRT || dur <= 0f)
        {
            if (_iconRT) _iconRT.localScale = Vector3.one;
            yield break;
        }

        Vector3 start = _iconRT.localScale;
        Vector3 target = Vector3.one;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float k = curve != null ? curve.Evaluate(p) : p;
            _iconRT.localScale = Vector3.LerpUnclamped(start, target, k);
            yield return null;
        }
        _iconRT.localScale = target;
        _iconPopCo = null;
    }

    IEnumerator FadeOverlay(float target, float dur, bool keepEnabledAtEnd)
    {
        float start = lockOverlay ? lockOverlay.color.a : 0f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, target, t / dur);
            SetOverlayAlpha(a);
            yield return null;
        }
        SetOverlayAlpha(target);
        if (!keepEnabledAtEnd) lockOverlay.enabled = false;
        _fadeCo = null;
    }

    void SetOverlayAlpha(float a)
    {
        if (!lockOverlay) return;
        var c = lockOverlay.color;
        c.a = Mathf.Clamp01(a);
        lockOverlay.color = c;
    }

    void ResetOwnerLabelDefaults()
    {
        if (!ownerText || !ownerRect || !ownerGroup) return;

        ownerText.enableAutoSizing = true;
        ownerText.fontSizeMin = 8;
        ownerText.fontSizeMax = 200;
        ownerText.textWrappingMode = TextWrappingModes.NoWrap;
        ownerText.overflowMode = TextOverflowModes.Truncate;
        ownerText.alignment = TextAlignmentOptions.MidlineLeft;

        var p = ownerRect.anchoredPosition;
        p.y = hiddenYOffset;
        ownerGroup.alpha = 0f;
        ownerText.text = string.Empty;
    }

    public void ShowOwnerName(string displayName)
    {
        if (!ownerText || !ownerRect || !ownerGroup) return;
        displayName = displayName ?? string.Empty;

        // If we are already showing this exact text and it's visible: do nothing.
        if (_ownerVisible && _ownerShownText == displayName && ownerGroup.alpha >= 0.99f)
            return;

        _ownerShownText = displayName;

        // Stop any opposite animation
        if (_ownerCo != null) { StopCoroutine(_ownerCo); _ownerCo = null; }

        // If we're already visible with the same text but mid-fade/slide, just snap to end.
        if (_ownerVisible && ownerText.text == displayName)
        {
            var to = ownerRect.anchoredPosition; to.y = 0f;
            ownerRect.anchoredPosition = to;
            ownerGroup.alpha = 1f;
            return;
        }

        ownerText.text = displayName;

        // Start the entry animation only if we’re active; otherwise snap to end-state.
        if (isActiveAndEnabled)
            _ownerCo = StartCoroutine(Co_ShowOwner());
        else
            SnapOwnerShown();
    }

    public void HideOwnerName()
    {
        if (!ownerText || !ownerRect || !ownerGroup) return;

        if (_ownerCo != null) { StopCoroutine(_ownerCo); _ownerCo = null; }

        // Already hidden? nothing to do.
        if (!_ownerVisible && ownerGroup.alpha <= 0.01f)
        {
            ownerText.text = string.Empty;
            return;
        }

        if (isActiveAndEnabled)
            _ownerCo = StartCoroutine(Co_HideOwner());
        else
            SnapOwnerHidden();
    }

    void SnapOwnerShown()
    {
        var p = ownerRect.anchoredPosition; p.y = 0f;
        ownerRect.anchoredPosition = p;
        ownerGroup.alpha = 1f;
        _ownerVisible = true;
    }
    void SnapOwnerHidden()
    {
        var p = ownerRect.anchoredPosition; p.y = hiddenYOffset;
        ownerRect.anchoredPosition = p;
        ownerGroup.alpha = 0f;
        _ownerVisible = false;
        _ownerShownText = null;
        ownerText.text = string.Empty;
    }


    IEnumerator Co_ShowOwner()
    {
        float t = 0f;
        Vector2 from = ownerRect.anchoredPosition;
        from.y = hiddenYOffset;
        Vector2 to = ownerRect.anchoredPosition;
        to.y = 0f;

        ownerRect.anchoredPosition = from;

        float fadeOverlap = Mathf.Min(fadeDuration, slideDuration) * 0.75f;

        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float p = ease.Evaluate(Mathf.Clamp01(t / slideDuration));
            ownerRect.anchoredPosition = Vector2.LerpUnclamped(from, to, p);

            float a = Mathf.Clamp01(t / fadeOverlap);
            ownerGroup.alpha = a;
            yield return null;
        }
        ownerRect.anchoredPosition = to;
        ownerGroup.alpha = 1f;
        _ownerCo = null;
        _ownerVisible = true;
    }

    IEnumerator Co_HideOwner()
    {
        float t = 0f;
        Vector2 from = ownerRect.anchoredPosition;
        Vector2 to = from; to.y = hiddenYOffset;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float p = ease.Evaluate(Mathf.Clamp01(t / fadeDuration));
            ownerGroup.alpha = 1f - p;
            ownerRect.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        ownerGroup.alpha = 0f;
        ownerRect.anchoredPosition = to;
        ownerText.text = string.Empty;
        _ownerCo = null;
        _ownerVisible = false;
        _ownerShownText = null;
    }

    void SafeStart(ref Coroutine handle, IEnumerator routine, System.Action immediateFallback)
    {
        if (handle != null) { StopCoroutine(handle); handle = null; }
        if (isActiveAndEnabled) handle = StartCoroutine(routine);
        else immediateFallback?.Invoke();
    }

    void OnDisable()
    {
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        if (_iconPopCo != null) { StopCoroutine(_iconPopCo); _iconPopCo = null; }
        if (_ownerCo != null) { StopCoroutine(_ownerCo); _ownerCo = null; }
        if (_scaleCo != null) { StopCoroutine(_scaleCo); _scaleCo = null; }
    }
}
