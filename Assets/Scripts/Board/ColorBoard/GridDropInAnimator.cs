using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class GridDropInAnimator : MonoBehaviour
{
    public enum ScheduleMode { UsePerCellMaxDelay, TotalDurationRandom, TotalDurationEven }
    public enum OffsetMode { RadialNormalized, Componentwise }

    public RectTransform gridRoot;
    public bool autoCollect = true;
    public List<RectTransform> cellRects = new List<RectTransform>();

    public ScheduleMode scheduleMode = ScheduleMode.TotalDurationRandom;
    public float perCellMaxDelay = 0.35f;
    public float dropDuration = 0.55f;
    public float totalDuration = 2.0f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool useUnscaledTime = true;

    public bool hideUntilDropStart = true;
    public bool fadeInDuringDrop = true;
    public float fadeInTime = 0f;

    public OffsetMode offsetMode = OffsetMode.RadialNormalized;
    public float startScale = 1.15f;
    public float distanceOffsetScaleX = 0.25f;
    public float distanceOffsetScaleY = 0.25f;
    public float maxOffsetX = 180f;
    public float maxOffsetY = 180f;
    public float extraDownDrop = 0f;

    public bool bringToFrontWithCanvas = true;
    public int animSortingOrder = 1000;

    public bool randomizeSeed = true;
    public int seed = 12345;

    public bool waitOneFrameForLayout = true;
    public int extraLayoutFrames = 0;
    public bool freezeLayoutDuringAnimation = true;
    public bool freezeByDisablingGrid = true;

    GridLayoutGroup _grid;
    ContentSizeFitter _fitter;
    bool _gridWasEnabled, _fitterWasEnabled;

    struct CellState
    {
        public RectTransform rt;
        public Vector2 basePos;
        public Vector3 baseScale;
        public Vector2 startPos;
        public Vector3 startScale;
        public float startTime;
        public float endTime;
        public float fadeEndTime;
        public bool started;
        public bool finished;

        public Canvas tempCanvas;
        public bool hadCanvasBefore;
        public bool prevOverrideSorting;
        public int prevSortingOrder;

        public CanvasGroup cg;

        public LayoutElement le;
        public bool hadLEBefore;
        public bool prevIgnoreLayout;
    }

    readonly List<CellState> _cells = new List<CellState>(512);
    Vector2 _center;
    bool _running;
    int _activeCount;

    void Awake()
    {
        if (!gridRoot) gridRoot = transform as RectTransform;
        _grid = gridRoot ? gridRoot.GetComponent<GridLayoutGroup>() : null;
        _fitter = gridRoot ? gridRoot.GetComponent<ContentSizeFitter>() : null;
    }

    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(Co_Play());
    }

    IEnumerator Co_Play()
    {
        if (waitOneFrameForLayout) yield return null;
        if (extraLayoutFrames > 0) for (int i = 0; i < extraLayoutFrames; i++) yield return null;

        Canvas.ForceUpdateCanvases();
        if (gridRoot) LayoutRebuilder.ForceRebuildLayoutImmediate(gridRoot);
        Canvas.ForceUpdateCanvases();

        PrepareCells();
        if (_cells.Count == 0) yield break;

        if (freezeLayoutDuringAnimation) BeginFreezeLayout();

        BuildSchedule();
        _running = true;
        _activeCount = _cells.Count;

        float endAt = Now() + GetWindow() + dropDuration + 0.05f;
        while (_running && Now() < endAt) yield return null;

        for (int i = 0; i < _cells.Count; i++)
        {
            var s = _cells[i];
            RestoreCanvas(ref s);
            RestoreLayoutElement(ref s);
            _cells[i] = s;
        }
        EndFreezeLayout();
        _running = false;
    }

    void PrepareCells()
    {
        _cells.Clear();

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
        if (cellRects.Count == 0) return;

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

            Vector2 dir = (basePos - _center);
            float dist = dir.magnitude;
            Vector2 dirN = dist > 1e-3f ? dir / dist : Vector2.zero;

            float ox, oy;
            if (offsetMode == OffsetMode.RadialNormalized)
                ox = dirN.x * dist * distanceOffsetScaleX;
            else
                ox = dir.x * distanceOffsetScaleX;

            if (offsetMode == OffsetMode.RadialNormalized)
                oy = dirN.y * dist * distanceOffsetScaleY;
            else
                oy = dir.y * distanceOffsetScaleY;

            if (maxOffsetX > 0f) ox = Mathf.Clamp(ox, -maxOffsetX, maxOffsetX);
            if (maxOffsetY > 0f) oy = Mathf.Clamp(oy, -maxOffsetY, maxOffsetY);
            Vector2 offset = new Vector2(ox, oy);
            offset.y -= extraDownDrop;

            rt.anchoredPosition = basePos + offset;
            rt.localScale = Vector3.one * startScale;

            CanvasGroup cg = null;
            if (hideUntilDropStart)
            {
                cg = rt.GetComponent<CanvasGroup>();
                if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            LayoutElement le = null;
            bool hadLEBefore = false;
            bool prevIgnoreLayout = false;
            if (freezeLayoutDuringAnimation && !freezeByDisablingGrid)
            {
                le = rt.GetComponent<LayoutElement>();
                if (!le) le = rt.gameObject.AddComponent<LayoutElement>();
                else hadLEBefore = true;
                prevIgnoreLayout = le.ignoreLayout;
                le.ignoreLayout = true;
            }

            CellState s = new CellState
            {
                rt = rt,
                basePos = basePos,
                baseScale = baseScale,
                startPos = basePos + offset,
                startScale = Vector3.one * startScale,
                started = false,
                finished = false,
                cg = cg,
                le = le,
                hadLEBefore = hadLEBefore,
                prevIgnoreLayout = prevIgnoreLayout
            };

            _cells.Add(s);
        }
    }

    void BeginFreezeLayout()
    {
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
        if (freezeByDisablingGrid)
        {
            if (_grid) _grid.enabled = _gridWasEnabled;
            if (_fitter) _fitter.enabled = _fitterWasEnabled;
        }
    }

    void BuildSchedule()
    {
        if (randomizeSeed) seed = System.Environment.TickCount;
        System.Random rng = new System.Random(seed);

        int n = _cells.Count;
        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        for (int i = 0; i < n; i++)
        {
            int j = rng.Next(i, n);
            int tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
        }

        float window = GetWindow();
        var now = Now();

        if (scheduleMode == ScheduleMode.TotalDurationEven)
        {
            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                float d = (n == 1) ? 0f : (window * k) / (n - 1);
                var s = _cells[i];
                s.startTime = now + d;
                s.endTime = s.startTime + dropDuration;
                s.fadeEndTime = s.startTime + (fadeInDuringDrop ? (fadeInTime > 0f ? fadeInTime : dropDuration) : 0f);
                _cells[i] = s;
            }
        }
        else
        {
            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                float d = (float)rng.NextDouble() * window;
                var s = _cells[i];
                s.startTime = now + d;
                s.endTime = s.startTime + dropDuration;
                s.fadeEndTime = s.startTime + (fadeInDuringDrop ? (fadeInTime > 0f ? fadeInTime : dropDuration) : 0f);
                _cells[i] = s;
            }
            if (scheduleMode == ScheduleMode.TotalDurationRandom && n > 0)
            {
                int last = order[n - 1];
                var s = _cells[last];
                s.startTime = now + window;
                s.endTime = s.startTime + dropDuration;
                s.fadeEndTime = s.startTime + (fadeInDuringDrop ? (fadeInTime > 0f ? fadeInTime : dropDuration) : 0f);
                _cells[last] = s;
            }
        }
    }

    float GetWindow()
    {
        switch (scheduleMode)
        {
            case ScheduleMode.UsePerCellMaxDelay: return Mathf.Max(0f, perCellMaxDelay);
            case ScheduleMode.TotalDurationRandom:
            case ScheduleMode.TotalDurationEven: return Mathf.Max(0f, totalDuration - dropDuration);
            default: return 0f;
        }
    }

    void Update()
    {
        if (!_running) return;

        var now = Now();
        bool anyActive = false;

        for (int i = 0; i < _cells.Count; i++)
        {
            var s = _cells[i];
            if (s.finished) continue;

            if (!s.started)
            {
                if (now < s.startTime) continue;
                s.started = true;
                if (bringToFrontWithCanvas)
                {
                    var existing = s.rt.GetComponent<Canvas>();
                    if (existing)
                    {
                        s.hadCanvasBefore = true;
                        s.prevOverrideSorting = existing.overrideSorting;
                        s.prevSortingOrder = existing.sortingOrder;
                        existing.overrideSorting = true;
                        existing.sortingOrder = animSortingOrder;
                        s.tempCanvas = existing;
                    }
                    else
                    {
                        var c = s.rt.gameObject.AddComponent<Canvas>();
                        c.overrideSorting = true;
                        c.sortingOrder = animSortingOrder;
                        s.tempCanvas = c;
                    }
                }
                else
                {
                    s.rt.SetAsLastSibling();
                }
                if (hideUntilDropStart && !fadeInDuringDrop && s.cg) s.cg.alpha = 1f;
            }

            anyActive = true;

            float p = Mathf.Clamp01((now - s.startTime) / dropDuration);
            float ep = ease.Evaluate(p);

            s.rt.anchoredPosition = Vector2.LerpUnclamped(s.startPos, s.basePos, ep);
            s.rt.localScale = Vector3.LerpUnclamped(s.startScale, s.baseScale, ep);

            if (hideUntilDropStart && fadeInDuringDrop && s.cg)
            {
                float fp = s.fadeEndTime > s.startTime ? Mathf.Clamp01((now - s.startTime) / (s.fadeEndTime - s.startTime)) : 1f;
                s.cg.alpha = fp;
            }

            if (p >= 1f)
            {
                s.rt.anchoredPosition = s.basePos;
                s.rt.localScale = s.baseScale;
                if (hideUntilDropStart && s.cg) s.cg.alpha = 1f;

                RestoreCanvas(ref s);
                RestoreLayoutElement(ref s);
                s.finished = true;
                _activeCount--;
            }

            _cells[i] = s;
        }

        if (!anyActive || _activeCount <= 0) _running = false;
    }

    void RestoreCanvas(ref CellState s)
    {
        if (!bringToFrontWithCanvas || s.rt == null) return;
        var c = s.tempCanvas;
        if (!c) return;

        if (s.hadCanvasBefore)
        {
            c.overrideSorting = s.prevOverrideSorting;
            c.sortingOrder = s.prevSortingOrder;
        }
        else
        {
            Destroy(c);
        }
        s.tempCanvas = null;
    }

    void RestoreLayoutElement(ref CellState s)
    {
        if (!freezeLayoutDuringAnimation || freezeByDisablingGrid) return;
        if (!s.rt) return;

        var le = s.le;
        if (!le) return;

        le.ignoreLayout = s.prevIgnoreLayout;
    }

    float Now() => useUnscaledTime ? Time.unscaledTime : Time.time;
}
