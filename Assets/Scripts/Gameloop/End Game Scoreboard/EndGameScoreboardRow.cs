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

    float _targetWidth;
    Coroutine _fillRoutine;

    public void Bind(string playerName, int score, Color color, float barWidth)
    {
        if (nameLabel) nameLabel.text = playerName;
        if (scoreLabel) scoreLabel.text = score.ToString();
        if (barImage) barImage.color = color;

        if (!barFill)
            return;

        _targetWidth = barWidth;

        if (_fillRoutine != null)
        {
            StopCoroutine(_fillRoutine);
            _fillRoutine = null;
        }

        if (animateFill && isActiveAndEnabled && gameObject.activeInHierarchy && fillDuration > 0f)
        {
            var size = barFill.sizeDelta;
            size.x = 0f;
            barFill.sizeDelta = size;

            _fillRoutine = StartCoroutine(CoFillBar());
        }
        else
        {
            var size = barFill.sizeDelta;
            size.x = _targetWidth;
            barFill.sizeDelta = size;
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

            yield return null;
        }

        var finalSize = barFill.sizeDelta;
        finalSize.x = _targetWidth;
        barFill.sizeDelta = finalSize;

        _fillRoutine = null;
    }

    void OnDisable()
    {
        if (_fillRoutine != null)
        {
            StopCoroutine(_fillRoutine);
            _fillRoutine = null;
        }
    }
}
