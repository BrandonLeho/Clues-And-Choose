using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmCancelSwapAnimator : MonoBehaviour
{
    [Header("Refs (CanvasGroup + RectTransform on each)")]
    public CanvasGroup confirmCG;
    public CanvasGroup cancelCG;

    [Header("Timing (seconds)")]
    public float outDuration = 0.10f;
    public float inDuration = 0.12f;
    public bool useUnscaledTime = true;

    [Header("Motion")]
    public float outYOffset = -12f;
    public float inYOffset = +8f;
    public float outScale = 0.96f;
    public float inOvershoot = 1.04f;

    [Header("Easing")]
    public AnimationCurve outEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve inEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    RectTransform _confirmRT, _cancelRT;
    Vector2 _confirmHome, _cancelHome;
    Coroutine _swapCo;

    void Awake()
    {
        if (confirmCG) _confirmRT = confirmCG.GetComponent<RectTransform>();
        if (cancelCG) _cancelRT = cancelCG.GetComponent<RectTransform>();

        if (_confirmRT) _confirmHome = _confirmRT.anchoredPosition;
        if (_cancelRT) _cancelHome = _cancelRT.anchoredPosition;

        SetImmediate(showCancel: false);
    }

    public void SetImmediate(bool showCancel, bool confirmInteractable = true)
    {
        StopSwapIfAny();

        if (confirmCG)
        {
            bool showConfirm = !showCancel;
            confirmCG.alpha = showConfirm ? 1f : 0f;
            confirmCG.interactable = showConfirm && confirmInteractable;
            confirmCG.blocksRaycasts = showConfirm && confirmInteractable;
            if (_confirmRT) _confirmRT.anchoredPosition = _confirmHome;
            if (_confirmRT) _confirmRT.localScale = Vector3.one;
        }

        if (cancelCG)
        {
            cancelCG.alpha = showCancel ? 1f : 0f;
            cancelCG.interactable = showCancel;
            cancelCG.blocksRaycasts = showCancel;
            if (_cancelRT) _cancelRT.anchoredPosition = _cancelHome;
            if (_cancelRT) _cancelRT.localScale = Vector3.one;
        }
    }

    public void SwapToConfirm(bool animate, bool confirmInteractable)
    {
        if (!confirmCG || !cancelCG) { SetImmediate(false, confirmInteractable); return; }
        if (animate) StartSwap(cancelCG, _cancelRT, _cancelHome, confirmCG, _confirmRT, _confirmHome, confirmInteractable, toCancel: false);
        else SetImmediate(false, confirmInteractable);
    }

    public void SwapToCancel(bool animate)
    {
        if (!confirmCG || !cancelCG) { SetImmediate(true); return; }
        StartSwap(confirmCG, _confirmRT, _confirmHome, cancelCG, _cancelRT, _cancelHome, /*confirmInteractable*/false, toCancel: true);
    }

    void StartSwap(CanvasGroup outCG, RectTransform outRT, Vector2 outHome,
                   CanvasGroup inCG, RectTransform inRT, Vector2 inHome,
                   bool confirmInteractableAfter, bool toCancel)
    {
        StopSwapIfAny();
        _swapCo = StartCoroutine(Co_Swap(outCG, outRT, outHome, inCG, inRT, inHome, confirmInteractableAfter, toCancel));
    }

    void StopSwapIfAny()
    {
        if (_swapCo != null) { StopCoroutine(_swapCo); _swapCo = null; }
    }

    IEnumerator Co_Swap(CanvasGroup outCG, RectTransform outRT, Vector2 outHome,
                        CanvasGroup inCG, RectTransform inRT, Vector2 inHome,
                        bool confirmInteractableAfter, bool toCancel)
    {
        float dt() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        outCG.interactable = false;
        outCG.blocksRaycasts = false;

        Vector2 outStartPos = outRT.anchoredPosition;
        Vector2 outEndPos = outHome + new Vector2(0f, outYOffset);
        float outStartA = outCG.alpha, outEndA = 0f;
        Vector3 outStartSc = outRT.localScale, outEndSc = Vector3.one * outScale;

        float t = 0f;
        while (t < outDuration)
        {
            t += dt();
            float p = Mathf.Clamp01(t / outDuration);
            float e = outEase.Evaluate(p);

            outRT.anchoredPosition = Vector2.LerpUnclamped(outStartPos, outEndPos, e);
            outRT.localScale = Vector3.LerpUnclamped(outStartSc, outEndSc, e);
            outCG.alpha = Mathf.LerpUnclamped(outStartA, outEndA, e);
            yield return null;
        }
        outRT.anchoredPosition = outEndPos;
        outRT.localScale = outEndSc;
        outCG.alpha = outEndA;

        inCG.interactable = false;
        inCG.blocksRaycasts = false;
        inCG.alpha = 0f;
        inRT.anchoredPosition = inHome + new Vector2(0f, inYOffset);
        inRT.localScale = Vector3.one * inOvershoot;

        t = 0f;
        while (t < inDuration)
        {
            t += dt();
            float p = Mathf.Clamp01(t / inDuration);
            float e = inEase.Evaluate(p);

            inRT.anchoredPosition = Vector2.LerpUnclamped(inHome + new Vector2(0f, inYOffset), inHome, e);
            inRT.localScale = Vector3.LerpUnclamped(Vector3.one * inOvershoot, Vector3.one, e);
            inCG.alpha = Mathf.LerpUnclamped(0f, 1f, e);
            yield return null;
        }
        inRT.anchoredPosition = inHome;
        inRT.localScale = Vector3.one;
        inCG.alpha = 1f;

        if (toCancel)
        {
            cancelCG.interactable = true;
            cancelCG.blocksRaycasts = true;

            confirmCG.interactable = false;
            confirmCG.blocksRaycasts = false;
        }
        else
        {
            confirmCG.interactable = confirmInteractableAfter;
            confirmCG.blocksRaycasts = confirmInteractableAfter;

            cancelCG.interactable = false;
            cancelCG.blocksRaycasts = false;
        }

        _swapCo = null;
    }
}
