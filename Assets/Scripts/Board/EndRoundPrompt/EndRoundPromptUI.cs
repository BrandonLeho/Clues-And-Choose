using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

[DisallowMultipleComponent]
public sealed class EndRoundPromptUI : MonoBehaviour
{
    public static EndRoundPromptUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] RectTransform panel;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Extra Elements")]
    [SerializeField] RectTransform[] extraElements;
    [SerializeField] float extraMoveUpOffset = 30f;

    [Header("Option Refs")]
    [SerializeField] EndRoundOptionHover yesOption;
    [SerializeField] EndRoundOptionHover noOption;

    [Header("Panel Animation")]
    [SerializeField] float duration = 0.45f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] float extraOffscreenPixels = 120f;

    [Header("State")]
    [SerializeField] bool startHidden = true;

    [Header("Impact Settings")]
    [SerializeField, Range(1.05f, 1.35f)] float impactScale = 1.18f;
    [SerializeField, Range(0.08f, 0.5f)] float impactDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] float impactExtraGlow = 0.4f;
    [SerializeField] float smallUpNudge = 10f;

    Vector2 shownAnchoredPos;
    Vector2 hiddenAnchoredPos;
    Vector2[] extraOriginals;
    Coroutine animCo;

    public static event Action<EndRoundOptionHover.OptionKind> OnChoiceDecided;

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

        if (extraElements != null && extraElements.Length > 0)
        {
            extraOriginals = new Vector2[extraElements.Length];
            for (int i = 0; i < extraElements.Length; i++)
                extraOriginals[i] = extraElements[i].anchoredPosition;
        }

        if (startHidden)
        {
            panel.anchoredPosition = hiddenAnchoredPos;
            if (canvasGroup) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
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
        if (canvasGroup) { canvasGroup.blocksRaycasts = true; canvasGroup.interactable = true; }
        PlayTo(true);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        if (canvasGroup) { canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
        PlayTo(false);
    }

    public void HideImmediate()
    {
        if (animCo != null) StopCoroutine(animCo);
        panel.anchoredPosition = hiddenAnchoredPos;
        if (canvasGroup) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
        if (extraElements != null)
        {
            for (int i = 0; i < extraElements.Length; i++)
                extraElements[i].anchoredPosition = extraOriginals[i];
        }

        ResetOptionVisuals();

        gameObject.SetActive(false);
    }

    void PlayTo(bool shown)
    {
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(CoSlide(shown));
    }

    IEnumerator CoSlide(bool toShown)
    {
        Vector2 pFrom = toShown ? hiddenAnchoredPos : shownAnchoredPos;
        Vector2 pTo = toShown ? shownAnchoredPos : hiddenAnchoredPos;

        float aFrom = toShown ? 0f : 1f;
        float aTo = toShown ? 1f : 0f;

        Vector2[] eFrom = null, eTo = null;
        if (extraElements != null && extraElements.Length > 0)
        {
            eFrom = new Vector2[extraElements.Length];
            eTo = new Vector2[extraElements.Length];
            for (int i = 0; i < extraElements.Length; i++)
            {
                var orig = extraOriginals[i];
                var up = orig + Vector2.up * extraMoveUpOffset;
                if (toShown) { eFrom[i] = orig; eTo[i] = up; }
                else { eFrom[i] = up; eTo[i] = orig; }
            }
        }

        panel.anchoredPosition = pFrom;
        if (canvasGroup) canvasGroup.alpha = aFrom;

        float t = 0f;
        float dur = Mathf.Max(0.0001f, duration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float e = ease.Evaluate(Mathf.Clamp01(t));

            panel.anchoredPosition = Vector2.LerpUnclamped(pFrom, pTo, e);
            if (canvasGroup) canvasGroup.alpha = Mathf.LerpUnclamped(aFrom, aTo, e);

            if (extraElements != null)
            {
                for (int i = 0; i < extraElements.Length; i++)
                    extraElements[i].anchoredPosition = Vector2.LerpUnclamped(eFrom[i], eTo[i], e);
            }
            yield return null;
        }

        panel.anchoredPosition = pTo;
        if (canvasGroup) canvasGroup.alpha = aTo;

        if (!toShown)
        {
            ResetOptionVisuals();
            gameObject.SetActive(false);
        }

        animCo = null;
    }

    public void PlayChoiceExit(EndRoundOptionHover.OptionKind choice)
    {
        StartCoroutine(CoImpactThenExit(choice));
    }

    IEnumerator CoImpactThenExit(EndRoundOptionHover.OptionKind choice)
    {
        if (canvasGroup) { canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }

        var chosen = (choice == EndRoundOptionHover.OptionKind.Yes) ? yesOption : noOption;

        Vector2 start = panel.anchoredPosition;
        Vector2 bumped = start + Vector2.up * smallUpNudge;
        float nt = 0f;
        while (nt < 1f)
        {
            nt += Time.unscaledDeltaTime / 0.08f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(nt));
            panel.anchoredPosition = Vector2.LerpUnclamped(start, bumped, e);
            yield return null;
        }
        panel.anchoredPosition = bumped;

        if (chosen != null)
            yield return chosen.PlayImpactBurst(impactScale, impactDuration, impactExtraGlow);

        OnChoiceDecided?.Invoke(choice);

        PlayTo(false);
        while (animCo != null) yield return null;

        ResetOptionVisuals();
    }

    void ResetOptionVisuals()
    {
        if (yesOption) yesOption.ResetVisuals();
        if (noOption) noOption.ResetVisuals();
    }
}
