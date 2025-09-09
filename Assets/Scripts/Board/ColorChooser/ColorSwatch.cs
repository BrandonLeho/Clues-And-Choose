using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

[RequireComponent(typeof(Button))]
public class ColorSwatch : MonoBehaviour, IPointerClickHandler
{
    [Header("Refs")]
    public Image fillImage;                   // read-only color block
    public SwatchHoverNeon hover;             // optional hover/selection scaler
    public RectTransform scaleTarget;         // what scales on hover/selection
    public Image lockOverlay;                 // lock icon image (position/size set in prefab)

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
    [SerializeField] TextMeshProUGUI ownerText;     // assign in Inspector (child under the banner)
    [SerializeField] RectTransform ownerRect;     // the rect of the text
    [SerializeField] CanvasGroup ownerGroup;    // CanvasGroup on the text for fades

    [Header("Owner Label Layout")]
    [SerializeField] float leftPadding = 8f;
    [SerializeField] float rightPadding = 8f;
    [SerializeField] float topPadding = 6f;
    [SerializeField] float bottomPadding = 6f;

    [Header("Owner Label Anim")]
    [SerializeField] float slideDuration = 0.25f;
    [SerializeField] float hiddenYOffset = -40f;    // start below the banner
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
            // start hidden
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
            // keep hover logic off only if locked, but still tell it about selection so it can hold selected scale
            hover.SetSelected(selected);
            hover.enabled = !IsLocked;
        }
    }

    internal void Lock()
    {
        if (IsLocked) return;
        IsLocked = true;

        if (_btn) _btn.interactable = false;
        if (hover) hover.enabled = false; // stop hover from driving scale

        // Smoothly scale banner back to normal (not instant)
        float normal = (hover != null) ? hover.normalScale : 1f;
        StartScaleTo(Vector3.one * normal, lockScaleDuration, lockScaleCurve);

        // Show lock overlay with fade + pop-in
        SetLockOverlayVisible(true);
    }

    internal void Unlock()
    {
        if (!IsLocked) return;
        IsLocked = false;

        if (_btn) _btn.interactable = true;
        if (hover) hover.enabled = true;

        // Hide lock overlay (fade out); keep icon size as-is
        SetLockOverlayVisible(false);
    }

    public Color GetFillColor() => fillImage ? fillImage.color : Color.white;

    // ---------- Banner scale anim ----------
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

        lockOverlay.enabled = true; // can tweak alpha even if parent inactive
        if (_iconPopCo != null) { StopCoroutine(_iconPopCo); _iconPopCo = null; }

        float targetA = on ? Mathf.Clamp01(lockOverlayAlpha) : 0f;

        // Icon pop-in
        if (on && iconPopIn && _iconRT)
        {
            if (isActiveAndEnabled)
            {
                _iconRT.localScale = Vector3.one * iconPopScale;
                _iconPopCo = StartCoroutine(IconPopInCo(iconPopDuration, iconPopCurve));
            }
            else
            {
                _iconRT.localScale = Vector3.one; // immediate end-state
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

        // Auto-size to fit, no wrap, no overflow
        ownerText.enableAutoSizing = true;
        ownerText.fontSizeMin = 8;     // tweak as you like
        ownerText.fontSizeMax = 200;   // tweak as you like
        ownerText.textWrappingMode = TextWrappingModes.NoWrap;
        ownerText.overflowMode = TextOverflowModes.Truncate; // never overflow visually
        ownerText.alignment = TextAlignmentOptions.MidlineLeft;

        var p = ownerRect.anchoredPosition;
        p.y = hiddenYOffset;
        ownerGroup.alpha = 0f;
        ownerText.text = string.Empty;
    }

    public void ShowOwnerName(string displayName)
    {
        if (!ownerText || !ownerRect || !ownerGroup) return;
        ownerText.text = displayName ?? string.Empty;

        SafeStart(ref _ownerCo, Co_ShowOwner(), () =>
        {
            // immediate end-state if inactive
            var p = ownerRect.anchoredPosition; p.y = 0f;
            ownerRect.anchoredPosition = p;
            ownerGroup.alpha = 1f;
        });
    }

    public void HideOwnerName()
    {
        if (!ownerText || !ownerRect || !ownerGroup) return;

        SafeStart(ref _ownerCo, Co_HideOwner(), () =>
        {
            // immediate end-state if inactive
            var p = ownerRect.anchoredPosition; p.y = hiddenYOffset;
            ownerRect.anchoredPosition = p;
            ownerGroup.alpha = 0f;
            ownerText.text = string.Empty;
        });
    }


    IEnumerator Co_ShowOwner()
    {
        // slide from hiddenYOffset -> 0, fade 0 -> 1
        float t = 0f;
        Vector2 from = ownerRect.anchoredPosition;
        from.y = hiddenYOffset;
        Vector2 to = ownerRect.anchoredPosition;
        to.y = 0f;

        // put it at start, visible anim
        ownerRect.anchoredPosition = from;

        // fade in slightly overlapped with slide
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
    }

    IEnumerator Co_HideOwner()
    {
        // fade 1 -> 0 while sliding a bit down
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
        ownerText.text = string.Empty; // “Otherwise don’t display the text.”
        _ownerCo = null;
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
