using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class BetPanelPopAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform panel;

    [Header("Positions (anchored)")]
    [SerializeField] Vector2 hiddenAnchoredPos = new Vector2(0f, -120f);

    [Header("Scale")]
    [SerializeField] float hiddenScale = 0.92f;

    [Header("Timing")]
    [SerializeField] float inDuration = 0.12f;
    [SerializeField] float outDuration = 0.10f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Behavior")]
    [SerializeField] bool disableRaycastsWhenHidden = true;

    Coroutine _co;
    bool _isShown;

    bool _initialized;
    Vector2 _shownAnchoredPos;
    float _shownScale;

    void Reset()
    {
        panel = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void EnsureInitialized()
    {
        if (_initialized) return;

        if (!panel) panel = transform as RectTransform;
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        _shownAnchoredPos = panel.anchoredPosition;

        float currentScale = panel.localScale.x;
        _shownScale = Mathf.Approximately(currentScale, 0f) ? 1f : currentScale;

        _initialized = true;
    }

    void Awake()
    {
        EnsureInitialized();
        ApplyInstantHidden();
    }

    public void Show()
    {
        if (_isShown) return;
        _isShown = true;
        StartTween(true);
    }

    public void Hide()
    {
        if (!_isShown) return;
        _isShown = false;
        StartTween(false);
    }

    public void ApplyInstantHidden()
    {
        EnsureInitialized();

        panel.anchoredPosition = hiddenAnchoredPos;
        panel.localScale = Vector3.one * (_shownScale * hiddenScale);
        canvasGroup.alpha = 0f;
        SetInteractable(false);
        _isShown = false;
    }

    void StartTween(bool toShown)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoTween(toShown));
    }

    IEnumerator CoTween(bool toShown)
    {
        EnsureInitialized();

        float d = Mathf.Max(0.0001f, toShown ? inDuration : outDuration);

        Vector2 p0 = panel.anchoredPosition;
        Vector2 p1 = toShown ? _shownAnchoredPos : hiddenAnchoredPos;

        float s0 = panel.localScale.x;
        float s1 = toShown ? _shownScale : _shownScale * hiddenScale;

        float a0 = canvasGroup.alpha;
        float a1 = toShown ? 1f : 0f;

        SetInteractable(toShown);

        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            float e = ease != null ? ease.Evaluate(u) : u;

            panel.anchoredPosition = Vector2.LerpUnclamped(p0, p1, e);
            float s = Mathf.LerpUnclamped(s0, s1, e);
            panel.localScale = Vector3.one * s;
            canvasGroup.alpha = Mathf.LerpUnclamped(a0, a1, e);

            yield return null;
        }

        panel.anchoredPosition = p1;
        panel.localScale = Vector3.one * s1;
        canvasGroup.alpha = a1;

        if (!toShown) SetInteractable(false);
        _co = null;
    }

    void SetInteractable(bool on)
    {
        if (!disableRaycastsWhenHidden) return;
        canvasGroup.blocksRaycasts = on;
        canvasGroup.interactable = on;
    }
}
