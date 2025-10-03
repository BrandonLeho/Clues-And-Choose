using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EndRoundPromptUI : MonoBehaviour
{
    public static EndRoundPromptUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] RectTransform panel;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField] float duration = 0.45f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] float extraOffscreenPixels = 120f;

    [Header("State")]
    [SerializeField] bool startHidden = true;

    Vector2 shownAnchoredPos;
    Vector2 hiddenAnchoredPos;
    Coroutine animCo;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!panel) panel = GetComponent<RectTransform>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
    }

    void Start()
    {
        shownAnchoredPos = panel.anchoredPosition;

        var h = GetCanvasHeight(panel);
        hiddenAnchoredPos = shownAnchoredPos + Vector2.down * (h + extraOffscreenPixels);

        if (startHidden)
        {
            panel.anchoredPosition = hiddenAnchoredPos;
            if (canvasGroup) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }

    static float GetCanvasHeight(RectTransform rt)
    {
        var canvas = rt.GetComponentInParent<Canvas>();
        if (canvas && canvas.pixelRect.height > 0) return canvas.pixelRect.height;

        var root = rt.root as RectTransform;
        return root ? root.rect.height : Screen.height;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        PlayTo(shown: true);
    }

    public void HideImmediate()
    {
        if (animCo != null) StopCoroutine(animCo);
        panel.anchoredPosition = hiddenAnchoredPos;
        if (canvasGroup) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    void PlayTo(bool shown)
    {
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(CoSlide(shown));
    }

    IEnumerator CoSlide(bool toShown)
    {
        Vector2 from = toShown ? hiddenAnchoredPos : shownAnchoredPos;
        Vector2 to = toShown ? shownAnchoredPos : hiddenAnchoredPos;

        float fromA = toShown ? 0f : 1f;
        float toA = toShown ? 1f : 0f;

        float t = 0f;
        panel.anchoredPosition = from;
        if (canvasGroup) canvasGroup.alpha = fromA;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
            float e = ease.Evaluate(Mathf.Clamp01(t));
            panel.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            if (canvasGroup) canvasGroup.alpha = Mathf.LerpUnclamped(fromA, toA, e);
            yield return null;
        }

        panel.anchoredPosition = to;
        if (canvasGroup) canvasGroup.alpha = toA;

        if (!toShown) gameObject.SetActive(false);
        animCo = null;
    }
}
