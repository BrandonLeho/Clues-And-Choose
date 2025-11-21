using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ScoreboardEntryAnimator : MonoBehaviour
{
    [Header("Slide In")]
    [SerializeField] RectTransform target;
    [SerializeField] float slideDistance = 600f;
    [SerializeField, Min(0f)] float slideDuration = 0.5f;
    [SerializeField] AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] bool playOnEnable = true;
    [SerializeField] bool useUnscaledTime = true;

    Vector2 _initialAnchoredPos;
    bool _initialized;
    Coroutine _slideRoutine;

    void Awake()
    {
        if (!target)
            target = GetComponent<RectTransform>();

        if (target)
        {
            _initialAnchoredPos = target.anchoredPosition;
            _initialized = true;
        }
    }

    void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    public void Play()
    {
        if (!target)
            return;

        if (!_initialized)
        {
            _initialAnchoredPos = target.anchoredPosition;
            _initialized = true;
        }

        if (_slideRoutine != null)
        {
            StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }

        _slideRoutine = StartCoroutine(CoSlideIn());
    }

    System.Collections.IEnumerator CoSlideIn()
    {
        Vector2 start = _initialAnchoredPos + new Vector2(0f, -Mathf.Abs(slideDistance));
        Vector2 end = _initialAnchoredPos;

        target.anchoredPosition = start;

        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = (slideDuration > 0f) ? Mathf.Clamp01(elapsed / slideDuration) : 1f;
            float curveT = (slideCurve != null) ? slideCurve.Evaluate(t) : t;

            target.anchoredPosition = Vector2.Lerp(start, end, curveT);
            yield return null;
        }

        target.anchoredPosition = end;
        _slideRoutine = null;
    }

    void OnDisable()
    {
        if (_slideRoutine != null)
        {
            StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }

        if (target && _initialized)
        {
            target.anchoredPosition = _initialAnchoredPos;
        }
    }
}
