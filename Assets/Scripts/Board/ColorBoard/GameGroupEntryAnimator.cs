using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class GameGroupEntryAnimator : MonoBehaviour
{
    [Header("Root (usually the Game Group)")]
    [Tooltip("CanvasGroup on the Game Group root. If null, one will be added.")]
    public CanvasGroup group;

    [Header("Entry Motion")]
    public float delay = 0.0f;
    public float duration = 0.6f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Start offset in anchored pixels (e.g., (0, -200) to slide up).")]
    public Vector2 startOffset = new Vector2(0f, -200f);
    public bool alsoScale = true;
    public Vector2 scaleRange = new Vector2(0.98f, 1.00f);

    [Header("Optional: Staggered child reveals")]
    public bool staggerChildren = false;
    [Tooltip("If empty, Graphics/TextMeshProUGUI under this object will be auto-collected.")]
    public List<Graphic> childGraphics = new List<Graphic>();
    public float childStaggerStep = 0.03f;
    public float childFadeTime = 0.25f;

    RectTransform rt;
    Vector2 homePos;
    public bool manageInput = true;
    public bool resetOnEnable = true;
    bool _prepared;

    void OnEnable()
    {
        if (resetOnEnable && !_prepared) PrepareHidden();
    }

    void Reset()
    {
        group = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (!group) group = GetComponent<CanvasGroup>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();

        homePos = rt.anchoredPosition;
        rt.anchoredPosition = homePos + startOffset;
        if (alsoScale) transform.localScale = Vector3.one * scaleRange.x;
        group.alpha = 0f;

        if (staggerChildren && (childGraphics == null || childGraphics.Count == 0))
        {
            childGraphics = new List<Graphic>(GetComponentsInChildren<Graphic>(true));
        }
        if (staggerChildren)
        {
            foreach (var g in childGraphics) if (g) SetGraphicAlpha(g, 0f);
        }
    }

    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(Co_Play());
    }

    IEnumerator Co_Play()
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float t = 0f;
        Vector2 start = homePos + startOffset;
        Vector2 end = homePos;
        float startScale = alsoScale ? scaleRange.x : 1f;
        float endScale = 1f;

        if (staggerChildren && childGraphics != null && childGraphics.Count > 0)
        {
            StartCoroutine(Co_StaggerChildren());
        }

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = ease.Evaluate(Mathf.Clamp01(t / duration));

            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, p);
            group.alpha = p;
            if (alsoScale) transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, endScale, p);

            yield return null;
        }

        rt.anchoredPosition = end;
        group.alpha = 1f;
        if (alsoScale) transform.localScale = Vector3.one;
        if (manageInput) { group.interactable = true; group.blocksRaycasts = true; }
    }

    IEnumerator Co_StaggerChildren()
    {
        for (int i = 0; i < childGraphics.Count; i++)
        {
            var g = childGraphics[i];
            if (g) StartCoroutine(Co_FadeGraphic(g, 0f, 1f, childFadeTime));
            yield return new WaitForSecondsRealtime(childStaggerStep);
        }
    }

    IEnumerator Co_FadeGraphic(Graphic g, float a0, float a1, float d)
    {
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / d);
            SetGraphicAlpha(g, Mathf.LerpUnclamped(a0, a1, p));
            yield return null;
        }
        SetGraphicAlpha(g, a1);
    }

    static void SetGraphicAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color; c.a = a; g.color = c;

        var tmp = g as TextMeshProUGUI;
        if (tmp) tmp.alpha = a;
    }

    void PrepareHidden()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        if (!rt) rt = GetComponent<RectTransform>();
        homePos = rt.anchoredPosition;
        rt.anchoredPosition = homePos + startOffset;
        group.alpha = 0f;
        if (alsoScale) transform.localScale = Vector3.one * scaleRange.x;
        if (manageInput) { group.interactable = false; group.blocksRaycasts = false; }
        _prepared = true;
    }
}
