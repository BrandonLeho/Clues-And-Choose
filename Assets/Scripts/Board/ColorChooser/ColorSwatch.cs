using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

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

    [Header("State (read-only)")]
    public bool IsSelected { get; private set; }
    public bool IsLocked { get; private set; }

    internal SelectionController owner;

    Button _btn;
    Coroutine _fadeCo;
    Coroutine _scaleCo;
    Coroutine _iconPopCo;
    RectTransform _iconRT;

    void Awake()
    {
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

    // ---------- Overlay (alpha + optional pop) ----------
    void SetLockOverlayVisible(bool on)
    {
        if (!lockOverlay) return;

        lockOverlay.enabled = true; // enable to animate
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        if (_iconPopCo != null) StopCoroutine(_iconPopCo);

        float targetA = on ? Mathf.Clamp01(lockOverlayAlpha) : 0f;

        if (on)
        {
            // Start from pop scale if requested
            if (iconPopIn && _iconRT)
            {
                _iconRT.localScale = Vector3.one * iconPopScale;
                _iconPopCo = StartCoroutine(IconPopInCo(iconPopDuration, iconPopCurve));
            }
        }

        if (fadeOverlay)
            _fadeCo = StartCoroutine(FadeOverlay(targetA, fadeDuration, on));
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
}
