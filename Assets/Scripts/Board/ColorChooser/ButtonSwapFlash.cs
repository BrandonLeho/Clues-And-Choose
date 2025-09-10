using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ButtonSwapFlash : MonoBehaviour
{
    [Header("Buttons (same parent)")]
    public Button confirmButton;
    public Button cancelButton;

    [Header("Flash Overlay")]
    public Image flashOverlay;              // full-rect, on top, Raycast Target = true

    [Header("Timings (seconds)")]
    public float flashIn = 0.06f;          // fade to full color
    public float hold = 0.04f;          // fully colored
    public float flashOut = 0.12f;          // fade back to clear

    [Header("Curves")]
    public AnimationCurve inCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve outCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Events")]
    public UnityEvent onAfterSwap;          // optional hook (SelectionController can re-run UI state)

    Coroutine _co;

    /// <summary>
    /// Briefly flash the whole button area with 'color', swap which button is active mid-flash.
    /// </summary>
    public void SwapTo(bool showCancel, Color color, System.Action afterSwap = null)
    {
        if (!isActiveAndEnabled)
        { // do it instantly if we're inactive
            ApplyActive(showCancel);
            afterSwap?.Invoke();
            onAfterSwap?.Invoke();
            return;
        }
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Swap(showCancel, color, afterSwap));
    }

    IEnumerator Co_Swap(bool showCancel, Color color, System.Action afterSwap)
    {
        // guard: ensure overlay exist & blocks input during the flash
        if (flashOverlay)
        {
            var c = color; c.a = 0f;
            flashOverlay.color = c;
            flashOverlay.raycastTarget = true;   // block clicks for the split second
            flashOverlay.gameObject.SetActive(true);
        }

        // Disable interaction on both while we transition
        if (confirmButton) confirmButton.interactable = false;
        if (cancelButton) cancelButton.interactable = false;

        // Phase 1: fade to solid color
        float t = 0f;
        while (flashOverlay && t < flashIn)
        {
            t += Time.unscaledDeltaTime;
            float p = inCurve.Evaluate(Mathf.Clamp01(t / flashIn));
            var c = color; c.a = p;
            flashOverlay.color = c;
            yield return null;
        }
        if (flashOverlay) { var c = color; c.a = 1f; flashOverlay.color = c; }

        // Midpoint: actually swap which button is visible
        ApplyActive(showCancel);

        // Phase 2: hold
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

        // Phase 3: fade out color
        t = 0f;
        while (flashOverlay && t < flashOut)
        {
            t += Time.unscaledDeltaTime;
            float p = outCurve.Evaluate(Mathf.Clamp01(t / flashOut));
            var c = color; c.a = 1f - p;
            flashOverlay.color = c;
            yield return null;
        }

        // Cleanup
        if (flashOverlay)
        {
            var c = flashOverlay.color; c.a = 0f;
            flashOverlay.color = c;
            flashOverlay.raycastTarget = false;
            flashOverlay.gameObject.SetActive(false);
        }

        afterSwap?.Invoke();
        onAfterSwap?.Invoke();
        _co = null;
    }

    void ApplyActive(bool showCancel)
    {
        if (confirmButton) confirmButton.gameObject.SetActive(!showCancel);
        if (cancelButton) cancelButton.gameObject.SetActive(showCancel);
    }
}
