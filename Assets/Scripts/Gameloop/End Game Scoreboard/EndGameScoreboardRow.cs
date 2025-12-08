using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndGameScoreboardRow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI scoreLabel;
    [SerializeField] RectTransform barFill;
    [SerializeField] Image barImage;

    [Header("Fill Animation")]
    [SerializeField] bool animateFill = true;
    [SerializeField, Min(0f)] float fillDuration = 0.6f;
    [SerializeField] AnimationCurve fillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] bool useUnscaledTime = true;

    [Header("Tip Glow")]
    [SerializeField] Image tipGlowImage;
    [SerializeField] bool animateTipGlow = true;
    [SerializeField, Range(0f, 5f)] float maxGlowIntensity = 1f;
    [SerializeField] AnimationCurve tipGlowAlphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] float tipGlowFadeOutDuration = 0.25f;

    float _targetWidth;
    Coroutine _fillRoutine;
    Coroutine _glowFadeRoutine;

    public void Bind(string playerName, int score, Color color, float barWidth)
    {
        if (nameLabel) nameLabel.text = playerName;
        if (scoreLabel) scoreLabel.text = score.ToString();
        if (barImage) barImage.color = color;

        if (tipGlowImage)
        {
            var c = color;
            c.a = tipGlowImage.color.a;
            tipGlowImage.color = c;
            tipGlowImage.enabled = true;
        }

        if (!barFill)
            return;

        _targetWidth = barWidth;

        if (_fillRoutine != null)
        {
            StopCoroutine(_fillRoutine);
            _fillRoutine = null;
        }

        if (_glowFadeRoutine != null)
        {
            StopCoroutine(_glowFadeRoutine);
            _glowFadeRoutine = null;
        }

        if (animateFill && isActiveAndEnabled && gameObject.activeInHierarchy && fillDuration > 0f)
        {
            var size = barFill.sizeDelta;
            size.x = 0f;
            barFill.sizeDelta = size;

            UpdateTipGlow(0f);

            _fillRoutine = StartCoroutine(CoFillBar());
        }
        else
        {
            var size = barFill.sizeDelta;
            size.x = _targetWidth;
            barFill.sizeDelta = size;

            UpdateTipGlow(1f);
        }
    }

    System.Collections.IEnumerator CoFillBar()
    {
        float elapsed = 0f;

        while (elapsed < fillDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = (fillDuration > 0f) ? Mathf.Clamp01(elapsed / fillDuration) : 1f;
            float curveT = (fillCurve != null) ? fillCurve.Evaluate(t) : t;

            float width = Mathf.Lerp(0f, _targetWidth, curveT);
            var size = barFill.sizeDelta;
            size.x = width;
            barFill.sizeDelta = size;

            UpdateTipGlow(curveT);

            yield return null;
        }

        var finalSize = barFill.sizeDelta;
        finalSize.x = _targetWidth;
        barFill.sizeDelta = finalSize;

        UpdateTipGlow(1f);

        if (animateTipGlow && tipGlowFadeOutDuration > 0f && tipGlowImage)
        {
            _glowFadeRoutine = StartCoroutine(CoFadeTipGlowOut());
        }

        _fillRoutine = null;
    }

    void UpdateTipGlow(float normalizedFill)
    {
        if (!tipGlowImage)
            return;

        if (!animateTipGlow)
        {
            var c = tipGlowImage.color;
            c.a = maxGlowIntensity;
            tipGlowImage.color = c;
            return;
        }

        float curveValue = tipGlowAlphaCurve != null
            ? tipGlowAlphaCurve.Evaluate(normalizedFill)
            : 1f;

        float intensity = Mathf.Clamp01(curveValue) * maxGlowIntensity;

        var col = tipGlowImage.color;
        col.a = intensity;
        tipGlowImage.color = col;
    }

    System.Collections.IEnumerator CoFadeTipGlowOut()
    {
        if (!tipGlowImage)
            yield break;

        float startAlpha = tipGlowImage.color.a;
        float elapsed = 0f;

        while (elapsed < tipGlowFadeOutDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = (tipGlowFadeOutDuration > 0f)
                ? Mathf.Clamp01(elapsed / tipGlowFadeOutDuration)
                : 1f;

            float alpha = Mathf.Lerp(startAlpha, 0f, t);
            var c = tipGlowImage.color;
            c.a = alpha;
            tipGlowImage.color = c;

            yield return null;
        }

        var final = tipGlowImage.color;
        final.a = 0f;
        tipGlowImage.color = final;

        _glowFadeRoutine = null;
    }

    void OnDisable()
    {
        if (_fillRoutine != null)
        {
            StopCoroutine(_fillRoutine);
            _fillRoutine = null;
        }

        if (_glowFadeRoutine != null)
        {
            StopCoroutine(_glowFadeRoutine);
            _glowFadeRoutine = null;
        }
    }
}
