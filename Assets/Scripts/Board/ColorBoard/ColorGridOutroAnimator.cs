using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ColorGridOutroAnimator : MonoBehaviour
{
    public enum OffsetMode { RadialNormalized, Componentwise }

    [Header("Grid")]
    public RectTransform gridRoot;
    public bool autoCollect = true;
    public List<RectTransform> cellRects = new List<RectTransform>();

    [Header("Timing")]
    [Min(0.05f)] public float perCellDuration = 0.9f;
    [Min(0.05f)] public float totalDuration = 3.0f;
    public bool useUnscaledTime = true;

    [Header("Scale & Fade (Per Cell)")]
    [Min(1f)] public float endScale = 1.6f;
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Offset")]
    public OffsetMode offsetMode = OffsetMode.RadialNormalized;
    public float distanceOffsetScaleX = 0.25f;
    public float distanceOffsetScaleY = 0.25f;
    public float maxOffsetX = 180f;
    public float maxOffsetY = 180f;
    public float extraDownOffset = 0f;
    public AnimationCurve offsetCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Canvas Group Fade")]
    public CanvasGroup gridCanvasGroup;
    public CanvasGroup scoreboardCanvasGroup;
    [Min(0f)] public float canvasFadeDelay = 0.5f;
    [Min(0.05f)] public float canvasFadeDuration = 0.5f;
    public AnimationCurve canvasFadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Extra World Fades")]
    public bool fadeCoinLockIconWithGrid = true;
    public SpriteRenderer coinLockIconRenderer;
    public SpriteRenderer coinLockBackgroundRenderer;

    public bool fadeCoinsWithGrid = true;
    public Transform coinsRoot;

    [Header("Coin Shrink")]
    [Min(0.05f)] public float coinShrinkDuration = 0.5f;
    public bool destroyCoinsOnShrinkComplete = true;

    [Header("Layout Freeze")]
    public bool freezeLayoutDuringAnimation = true;
    public bool freezeByDisablingGrid = true;

    [Header("Hover Lock")]
    public bool lockAllCellHoversOnPlay = true;
    public bool unlockHoversOnComplete = false;

    [Header("Options")]
    public bool disableRaycasts = true;
    public bool randomizeSeed = true;
    public int seed = 12345;

    [Header("Events")]
    public UnityEvent OnAnimationComplete;

    class CellState
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public Vector3 baseScale;
        public Vector2 basePos;
        public Vector2 offset;
        public float startTime;
        public bool started;
        public bool finished;
    }

    readonly List<CellState> _cells = new List<CellState>(512);
    bool _running;
    int _activeCount;
    Vector2 _center;

    GridLayoutGroup _grid;
    ContentSizeFitter _fitter;
    bool _gridWasEnabled, _fitterWasEnabled;

    void Awake()
    {
        if (!gridRoot)
            gridRoot = transform as RectTransform;

        if (gridRoot)
        {
            _grid = gridRoot.GetComponent<GridLayoutGroup>();
            _fitter = gridRoot.GetComponent<ContentSizeFitter>();
        }
    }

    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(Co_Play());
    }

    IEnumerator Co_Play()
    {
        if (lockAllCellHoversOnPlay)
            HardDisableAllCellHoversNowLikeDimmer();

        PrepareCells();
        if (_cells.Count == 0)
        {
            if (lockAllCellHoversOnPlay && unlockHoversOnComplete)
                ReEnableHoversLikeDimmer();

            OnAnimationComplete?.Invoke();
            yield break;
        }

        BeginFreezeLayout();
        BuildSchedule();

        if (gridCanvasGroup)
            StartCoroutine(Co_FadeCanvasGroups());

        _running = true;
        _activeCount = _cells.Count;

        while (_running)
            yield return null;

        EndFreezeLayout();
        Canvas.ForceUpdateCanvases();
        if (gridRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRoot);
        Canvas.ForceUpdateCanvases();

        if (lockAllCellHoversOnPlay && unlockHoversOnComplete)
            ReEnableHoversLikeDimmer();

        OnAnimationComplete?.Invoke();
    }

    void PrepareCells()
    {
        _cells.Clear();

        if (!gridRoot)
            return;

        if (autoCollect)
        {
            cellRects.Clear();
            var imgs = gridRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                var img = imgs[i];
                if (!img) continue;
                var rt = img.transform as RectTransform;
                if (!rt || rt == (RectTransform)transform) continue;
                cellRects.Add(rt);
            }
        }

        if (cellRects.Count == 0)
            return;

        Vector2 min = cellRects[0].anchoredPosition;
        Vector2 max = min;
        for (int i = 0; i < cellRects.Count; i++)
        {
            var p = cellRects[i].anchoredPosition;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        _center = (min + max) * 0.5f;

        for (int i = 0; i < cellRects.Count; i++)
        {
            var rt = cellRects[i];
            if (!rt) continue;

            var basePos = rt.anchoredPosition;
            var baseScale = rt.localScale;

            Vector2 dir = basePos - _center;
            float dist = dir.magnitude;
            Vector2 dirN = dist > 1e-3f ? dir / dist : Vector2.zero;

            float ox, oy;
            if (offsetMode == OffsetMode.RadialNormalized)
            {
                ox = dirN.x * dist * distanceOffsetScaleX;
                oy = dirN.y * dist * distanceOffsetScaleY;
            }
            else
            {
                ox = dir.x * distanceOffsetScaleX;
                oy = dir.y * distanceOffsetScaleY;
            }

            if (maxOffsetX > 0f) ox = Mathf.Clamp(ox, -maxOffsetX, maxOffsetX);
            if (maxOffsetY > 0f) oy = Mathf.Clamp(oy, -maxOffsetY, maxOffsetY);

            Vector2 offset = new Vector2(ox, oy);
            offset.y -= extraDownOffset;

            var cg = rt.GetComponent<CanvasGroup>();
            if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();

            cg.alpha = 1f;
            if (disableRaycasts)
            {
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            _cells.Add(new CellState
            {
                rt = rt,
                cg = cg,
                baseScale = baseScale,
                basePos = basePos,
                offset = offset,
                started = false,
                finished = false
            });
        }
    }

    void BuildSchedule()
    {
        int n = _cells.Count;
        if (n == 0) return;

        System.Random rng = new System.Random(randomizeSeed ? System.Environment.TickCount : seed);

        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        for (int i = 0; i < n; i++)
        {
            int j = rng.Next(i, n);
            int tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
        }

        float window = Mathf.Max(0f, totalDuration - perCellDuration);
        float now = Now();

        for (int k = 0; k < n; k++)
        {
            int idx = order[k];
            var s = _cells[idx];

            float d = (n == 1) ? 0f : (float)rng.NextDouble() * window;
            s.startTime = now + d;
        }
    }

    void Update()
    {
        if (!_running) return;

        float now = Now();
        bool anyActive = false;

        for (int i = 0; i < _cells.Count; i++)
        {
            var s = _cells[i];
            if (s.finished) continue;

            anyActive = true;

            if (!s.started)
            {
                if (now < s.startTime) continue;
                s.started = true;
            }

            float t = Mathf.Clamp01((now - s.startTime) / perCellDuration);

            float su = scaleCurve.Evaluate(t);
            float fu = fadeCurve.Evaluate(t);
            float ou = offsetCurve != null ? offsetCurve.Evaluate(t) : su;

            if (s.rt)
            {
                s.rt.localScale = s.baseScale * Mathf.Lerp(1f, endScale, su);
                s.rt.anchoredPosition = s.basePos + s.offset * ou;
            }

            if (s.cg)
                s.cg.alpha = fu;

            if (t >= 1f)
            {
                if (s.rt)
                {
                    s.rt.localScale = s.baseScale * endScale;
                    s.rt.anchoredPosition = s.basePos + s.offset;
                }

                if (s.cg)
                    s.cg.alpha = 0f;

                s.finished = true;
                _activeCount--;
            }
        }

        if (!anyActive || _activeCount <= 0)
            _running = false;
    }

    IEnumerator Co_FadeCanvasGroups()
    {
        float startTime = Now() + canvasFadeDelay;
        while (Now() < startTime)
            yield return null;

        bool hasIcon = fadeCoinLockIconWithGrid && coinLockIconRenderer;
        bool hasBg = fadeCoinLockIconWithGrid && coinLockBackgroundRenderer;

        Color iconBaseColor = hasIcon ? coinLockIconRenderer.color : default;
        Color bgBaseColor = hasBg ? coinLockBackgroundRenderer.color : default;

        List<Transform> coinTransforms = null;
        List<Vector3> coinBaseScales = null;

        if (fadeCoinsWithGrid && coinsRoot)
        {
            var coinVisuals = new List<CoinVisual>();
            coinsRoot.GetComponentsInChildren(true, coinVisuals);

            if (coinVisuals.Count > 0)
            {
                coinTransforms = new List<Transform>(coinVisuals.Count);
                coinBaseScales = new List<Vector3>(coinVisuals.Count);

                for (int i = 0; i < coinVisuals.Count; i++)
                {
                    var cv = coinVisuals[i];
                    if (!cv) continue;

                    var tf = cv.transform;
                    coinTransforms.Add(tf);
                    coinBaseScales.Add(tf.localScale);
                }
            }
        }

        float fadeDuration = Mathf.Max(0.01f, canvasFadeDuration);
        float shrinkDuration = Mathf.Max(0.01f, coinShrinkDuration);

        float t0 = Now();
        float fadeEnd = t0 + fadeDuration;
        float shrinkEnd = t0 + shrinkDuration;
        float endTime = Mathf.Max(fadeEnd, shrinkEnd);

        bool coinsDestroyedOnShrink = false;

        while (Now() < endTime)
        {
            float now = Now();
            float elapsed = now - t0;

            float fadeT = Mathf.Clamp01(elapsed / fadeDuration);
            float a = canvasFadeCurve.Evaluate(fadeT);

            if (gridCanvasGroup)
                gridCanvasGroup.alpha = a;

            if (hasIcon)
            {
                var c = iconBaseColor;
                c.a = iconBaseColor.a * a;
                coinLockIconRenderer.color = c;
            }

            if (hasBg)
            {
                var c = bgBaseColor;
                c.a = bgBaseColor.a * a;
                coinLockBackgroundRenderer.color = c;
            }

            if (coinTransforms != null && coinBaseScales != null)
            {
                float shrinkT = Mathf.Clamp01(elapsed / shrinkDuration);
                float shrinkFactor = canvasFadeCurve.Evaluate(shrinkT);

                for (int i = 0; i < coinTransforms.Count; i++)
                {
                    var tf = coinTransforms[i];
                    if (!tf) continue;

                    var baseScale = coinBaseScales[i];
                    tf.localScale = baseScale * shrinkFactor;
                }

                if (!coinsDestroyedOnShrink && shrinkT >= 1f && destroyCoinsOnShrinkComplete)
                {
                    coinsDestroyedOnShrink = true;

                    if (coinsRoot)
                        Destroy(coinsRoot.gameObject);
                }
            }

            yield return null;
        }

        if (gridCanvasGroup)
        {
            gridCanvasGroup.alpha = 0f;
            gridCanvasGroup.interactable = false;
            gridCanvasGroup.blocksRaycasts = false;
            gridCanvasGroup.gameObject.SetActive(false);
        }

        if (hasIcon)
        {
            var c = iconBaseColor;
            c.a = 0f;
            coinLockIconRenderer.color = c;
        }

        if (hasBg)
        {
            var c = bgBaseColor;
            c.a = 0f;
            coinLockBackgroundRenderer.color = c;
        }

        if (fadeCoinsWithGrid && coinsRoot && !destroyCoinsOnShrinkComplete)
        {
            var coinVisuals = new List<CoinVisual>();
            coinsRoot.GetComponentsInChildren(true, coinVisuals);

            for (int i = 0; i < coinVisuals.Count; i++)
            {
                var cv = coinVisuals[i];
                if (!cv) continue;

                cv.transform.localScale = Vector3.zero;
            }
        }

        if (scoreboardCanvasGroup)
        {
            scoreboardCanvasGroup.gameObject.SetActive(true);
            scoreboardCanvasGroup.alpha = 1f;
            scoreboardCanvasGroup.interactable = true;
            scoreboardCanvasGroup.blocksRaycasts = true;
        }
    }

    void BeginFreezeLayout()
    {
        if (!freezeLayoutDuringAnimation) return;
        if (!freezeByDisablingGrid) return;

        if (_grid)
        {
            _gridWasEnabled = _grid.enabled;
            _grid.enabled = false;
        }
        if (_fitter)
        {
            _fitterWasEnabled = _fitter.enabled;
            _fitter.enabled = false;
        }
    }

    void EndFreezeLayout()
    {
        if (!freezeLayoutDuringAnimation) return;
        if (!freezeByDisablingGrid) return;

        if (_grid)
            _grid.enabled = _gridWasEnabled;
        if (_fitter)
            _fitter.enabled = _fitterWasEnabled;
    }

    float Now() => useUnscaledTime ? Time.unscaledTime : Time.time;

    void HardDisableAllCellHoversNowLikeDimmer()
    {
        GridHoverRelay.Instance?.HoverExit();

        var hovers = FindObjectsByType<GridCellHoverWithCoords>(FindObjectsSortMode.None);
        for (int i = 0; i < hovers.Length; i++)
        {
            var h = hovers[i];
            if (!h) continue;

            if (h.IsHoverLocked)
            {
                h.SetHoverLock(false, keepShown: false);
                h.ProbeEnter();
            }

            h.ProbeExit();
            h.SetHoverEnabled(false);
        }

        if (gridRoot)
        {
            var enablers = gridRoot.GetComponents<EnableAllCellHoversAfterFlyIn>();
            for (int i = 0; i < enablers.Length; i++)
                if (enablers[i]) enablers[i].enabled = false;
        }
    }

    void ReEnableHoversLikeDimmer()
    {
        var dimmer = GridDimmerOverlay.Instance;
        if (dimmer != null)
            dimmer.EnableAllCellHovers();
        else
            ToggleAllCellHovers(true);
    }

    void ToggleAllCellHovers(bool enabled)
    {
        if (!gridRoot) return;

        var hovers = gridRoot.GetComponentsInChildren<GridCellHoverWithCoords>(true);
        for (int i = 0; i < hovers.Length; i++)
        {
            var h = hovers[i];
            if (!h) continue;

            h.SetHoverEnabled(enabled);
        }
    }
}
