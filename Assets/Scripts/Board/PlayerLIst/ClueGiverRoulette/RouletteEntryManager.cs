using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class RouletteEntryManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform rouletteRoot;
    [SerializeField] ColorGridAnimator grid;
    [SerializeField] RouletteNetSync netSync;
    [SerializeField] RouletteText roulette;

    [Header("Timing")]
    [Tooltip("Extra delay AFTER the grid finishes before showing roulette.")]
    [SerializeField] float delayAfterGrid = 0.35f;

    [Tooltip("Duration for the roulette panel to slide up into place.")]
    [SerializeField] float enterDuration = 0.45f;

    [Tooltip("Duration for the roulette panel to slide back down off-screen.")]
    [SerializeField] float exitDuration = 0.45f;

    [Tooltip("Call RequestSpin() this many seconds BEFORE the entry animation starts.")]
    [Min(0f)]
    [SerializeField] float spinLeadBeforeEnter = 0.25f;

    [Tooltip("Wait this long AFTER the final choice (and optional winner animation) before sliding down.")]
    [Min(0f)]
    [SerializeField] float extraHoldAfterFinalChoice = 0.35f;

    [Tooltip("Wait for the roulette's built-in winner animation before sliding down?")]
    [SerializeField] bool waitForWinnerAnimation = true;

    [Tooltip("Use unscaled time (ignores Time.timeScale).")]
    [SerializeField] bool useUnscaledTime = true;

    [Header("Motion")]
    [SerializeField] AnimationCurve enterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] AnimationCurve exitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("How far below its anchored position is 'off-screen'.")]
    [SerializeField] float offscreenExtraPadding = 160f;

    [Header("Hooks")]
    public UnityEvent OnRouletteShown;
    public UnityEvent OnRouletteSpinRequested;
    public UnityEvent OnRouletteHidden;

    Vector2 _homePos;
    Vector2 _offPos;
    bool _isAnimating;
    bool _waitingSpinDone;

    void Reset()
    {
        rouletteRoot = GetComponent<RectTransform>();
        if (!roulette) roulette = GetComponentInChildren<RouletteText>(true);
        if (!netSync) netSync = GetComponentInChildren<RouletteNetSync>(true);
    }

    void Awake()
    {
        if (!rouletteRoot) rouletteRoot = transform as RectTransform;
        _homePos = rouletteRoot.anchoredPosition;
        _offPos = _homePos + new Vector2(0f, -(GetParentHeight(rouletteRoot) + offscreenExtraPadding));
        rouletteRoot.anchoredPosition = _offPos;
    }

    void OnEnable()
    {
        if (grid)
        {
            grid.OnAnimationComplete.RemoveListener(OnGridFinished);
            grid.OnAnimationComplete.AddListener(OnGridFinished);
        }
        if (roulette)
        {
            roulette.OnSpinComplete.RemoveListener(OnSpinComplete);
            roulette.OnSpinComplete.AddListener(OnSpinComplete);
        }
    }

    void OnDisable()
    {
        if (grid) grid.OnAnimationComplete.RemoveListener(OnGridFinished);
        if (roulette) roulette.OnSpinComplete.RemoveListener(OnSpinComplete);
    }

    void OnGridFinished()
    {
        if (!_isAnimating) StartCoroutine(Co_ShowSpinHide());
    }

    IEnumerator Co_ShowSpinHide()
    {
        _isAnimating = true;

        float preSpinWait = Mathf.Max(0f, delayAfterGrid - spinLeadBeforeEnter);
        float remainingToEnter = Mathf.Max(0f, delayAfterGrid - preSpinWait);

        yield return WaitForSecondsFlex(preSpinWait);

        if (netSync) netSync.RequestSpin();
        OnRouletteSpinRequested?.Invoke();

        yield return WaitForSecondsFlex(remainingToEnter);

        yield return Co_Slide(rouletteRoot, _offPos, _homePos, enterDuration, enterCurve);
        OnRouletteShown?.Invoke();

        _waitingSpinDone = true;
        while (_waitingSpinDone) yield return null;

        if (waitForWinnerAnimation && roulette && roulette.enableWinnerAnimation)
        {
            float total = Mathf.Max(0f, roulette.winnerDelay) + Mathf.Max(0.001f, roulette.winnerAnimDuration) + 0.05f;
            yield return WaitForSecondsFlex(total);
        }

        yield return WaitForSecondsFlex(extraHoldAfterFinalChoice);

        yield return Co_Slide(rouletteRoot, _homePos, _offPos, exitDuration, exitCurve);
        OnRouletteHidden?.Invoke();

        _isAnimating = false;
    }

    void OnSpinComplete(string winner, int index)
    {
        _waitingSpinDone = false;
    }

    IEnumerator Co_Slide(RectTransform rt, Vector2 from, Vector2 to, float dur, AnimationCurve curve)
    {
        float t = 0f;
        dur = Mathf.Max(0.0001f, dur);
        while (t < 1f)
        {
            t += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) / dur;
            float p = curve.Evaluate(Mathf.Clamp01(t));
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    object WaitForSecondsFlex(float seconds)
    {
        if (seconds <= 0f) return null;
        return useUnscaledTime ? (object)new WaitForSecondsRealtime(seconds) : new WaitForSeconds(seconds);
    }

    float GetParentHeight(RectTransform child)
    {
        var parent = child.parent as RectTransform;
        return parent ? parent.rect.height : Screen.height;
    }

    [ContextMenu("Test Sequence")]
    public void TestSequence()
    {
        if (!_isAnimating) StartCoroutine(Co_ShowSpinHide());
    }
}
