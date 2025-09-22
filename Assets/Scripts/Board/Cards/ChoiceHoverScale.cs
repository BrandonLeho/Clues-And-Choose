using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class ChoiceHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Range(1f, 1.3f)] public float hoverScale = 1.08f;
    public float duration = 0.12f;
    [Range(0f, 1f)] public float easeOut = 0.6f;

    RectTransform rt;
    Vector3 baseScale;
    Coroutine anim;

    void Awake()
    {
        rt = (RectTransform)transform;
        baseScale = rt.localScale;
    }

    void OnEnable()
    {
        if (anim != null) StopCoroutine(anim);
        rt.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Play(toHover: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Play(toHover: false);
    }

    void Play(bool toHover)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(CoScale(toHover));
    }

    IEnumerator CoScale(bool toHover)
    {
        Vector3 start = rt.localScale;
        Vector3 end = toHover ? baseScale * hoverScale : baseScale;

        float t = 0f, d = Mathf.Max(0.0001f, duration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / d);
            float k = Mathf.Lerp(0f, 3f, easeOut);
            float eased = 1f - Mathf.Pow(1f - p, k + 1f);
            rt.localScale = Vector3.LerpUnclamped(start, end, eased);
            yield return null;
        }
        rt.localScale = end;
        anim = null;
    }
}
