using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HorizontalTextRoulette : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform viewport;
    public RectTransform content;
    public RouletteTextItem textItemPrefab;

    [Header("Data")]
    public List<string> entries = new List<string>();

    [Header("Layout")]
    public float itemHorizontalPadding = 36f;
    public float spacing = 24f;
    public float baselineY = 0f;
    public bool useFixedHeight = true;

    [Header("Spin (constant)")]
    public float spinSpeed = 1500f;   // px/sec while spinning

    [Header("Timing")]
    [Tooltip("Total time from StartSpin() to landing (pre-spin + decel).")]
    public float totalSpinDuration = 2.5f;
    [Tooltip("Smallest allowed decel time if StopAt() is called late.")]
    public float minDecelDuration = 0.25f;
    [Tooltip("Use realtime (ignores timescale) or game time.")]
    public bool useUnscaledTime = true;

    [Header("Center Marker")]
    public bool showCenterGuide = false;
    public float markerOffsetX = 0f;

    public event Action<int, string> OnStopped;

    enum State { Idle, Spinning, Decelerating, Stopped }
    State _state = State.Idle;

    readonly List<RouletteTextItem> _active = new();
    readonly List<float> _activeWidths = new();

    float _contentX;
    float _currSpeed;

    // spin timing
    float _spinStartTime = -1f;

    // Decel (Hermite: s(0)=0, s'(0)=v0*T, s(1)=D, s'(1)=0)
    int _stopIndex;
    int _extraPasses;
    float _decelDuration;
    float _decelT;
    float _decelTotalDist;
    float _decelInitialSpeed;
    float _hermitePrevS;

    float Now => useUnscaledTime ? Time.unscaledTime : Time.time;
    float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    void Awake()
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[Roulette] No entries.");
            return;
        }
        BuildStrip();
        ResetToStart();
    }

    void Update()
    {
        if (_state == State.Idle || entries.Count == 0) return;

        float dt = Dt;

        if (_state == State.Spinning)
        {
            float move = _currSpeed * dt;
            _contentX -= move;
            content.anchoredPosition = new Vector2(_contentX, content.anchoredPosition.y);
            RecycleItemsIfNeeded();
            return;
        }

        if (_state == State.Decelerating)
        {
            _decelT += dt / Mathf.Max(0.0001f, _decelDuration);
            float u = Mathf.Clamp01(_decelT);

            float T = _decelDuration;
            float D = _decelTotalDist;
            float v0 = _decelInitialSpeed;

            float u2 = u * u;
            float u3 = u2 * u;

            // s(u) = (u^3 - 2u^2 + u) * (T*v0) + (-2u^3 + 3u^2) * D
            float s = (u3 - 2f * u2 + u) * (T * v0) + (-2f * u3 + 3f * u2) * D;
            s = Mathf.Min(s, D);

            float delta = s - _hermitePrevS;
            _hermitePrevS = s;

            _contentX -= delta;
            content.anchoredPosition = new Vector2(_contentX, content.anchoredPosition.y);
            RecycleItemsIfNeeded();

            if (u >= 1f - 1e-4f)
            {
                _state = State.Stopped;
                _currSpeed = 0f;

                AlignChosenToMarker(_stopIndex);
                OnStopped?.Invoke(_stopIndex, entries[_stopIndex]);
            }
        }
    }

    // ------------------------- Public API -------------------------

    public void SetEntries(List<string> newEntries, bool rebuild = true)
    {
        entries = newEntries ?? new List<string>();
        if (rebuild)
        {
            ClearActive();
            BuildStrip();
            ResetToStart();
        }
    }

    /// Start spinning at constant speed; total timing begins now.
    public void StartSpin(float initialSpeed = -1f)
    {
        if (entries == null || entries.Count == 0) return;

        _state = State.Spinning;
        _currSpeed = (initialSpeed > 0f) ? initialSpeed : spinSpeed;

        _spinStartTime = Now;   // <-- timing start
    }

    public void SetSpeed(float newSpeed)
    {
        spinSpeed = Mathf.Max(0f, newSpeed);
        if (_state == State.Spinning) _currSpeed = spinSpeed;
    }

    /// Decelerate to stop on index after extraPasses, using remaining time so total = totalSpinDuration.
    public void StopAt(int index, int extraPasses)
    {
        if (entries == null || entries.Count == 0) return;
        if (_state == State.Stopped) return;

        _stopIndex = Mod(index, entries.Count);
        _extraPasses = Mathf.Max(0, extraPasses);

        // Compute remaining time for decel so (StartSpin -> stop) == totalSpinDuration
        float elapsed = (_spinStartTime >= 0f) ? Mathf.Max(0f, Now - _spinStartTime) : 0f;
        float remaining = Mathf.Max(minDecelDuration, totalSpinDuration - elapsed);

        BeginDecelWithDuration(remaining);
    }

    public void StopAt(string label, int extraPasses)
    {
        int idx = entries.IndexOf(label);
        if (idx < 0) idx = 0;
        StopAt(idx, extraPasses);
    }

    /// One-call helper: spins now and auto-stops so total time == totalSpinDuration.
    /// 'decelPortion' is the fraction of total reserved for decel (0.5–0.8 feels good).
    public void StartSpinAndAutoStop(int index, int extraPasses = 1, float decelPortion = 0.6f, float initialSpeed = -1f)
    {
        StartSpin(initialSpeed);
        float decelDur = Mathf.Clamp(totalSpinDuration * Mathf.Clamp01(decelPortion), minDecelDuration, totalSpinDuration - 0.01f);
        float preSpin = Mathf.Max(0f, totalSpinDuration - decelDur);
        StartCoroutine(Co_AutoStop(index, extraPasses, preSpin, decelDur));
    }

    public void StartSpinAndAutoStop(string label, int extraPasses = 1, float decelPortion = 0.6f, float initialSpeed = -1f)
    {
        int idx = entries.IndexOf(label);
        if (idx < 0) idx = 0;
        StartSpinAndAutoStop(idx, extraPasses, decelPortion, initialSpeed);
    }

    public void ResetToStart()
    {
        _state = State.Idle;
        _currSpeed = 0f;
        _contentX = 0f;
        content.anchoredPosition = new Vector2(_contentX, content.anchoredPosition.y);
        _spinStartTime = -1f;
        _decelT = 0f;
        _hermitePrevS = 0f;
    }

    // ---------------------- Internals ----------------------

    IEnumerator Co_AutoStop(int index, int extraPasses, float preSpin, float decelDur)
    {
        if (useUnscaledTime) yield return new WaitForSecondsRealtime(preSpin);
        else yield return new WaitForSeconds(preSpin);

        // Prepare chosen index and compute distance, then decel with fixed duration
        _stopIndex = Mod(index, entries.Count);
        _extraPasses = Mathf.Max(0, extraPasses);
        BeginDecelWithDuration(decelDur);
    }

    void BeginDecelWithDuration(float duration)
    {
        float distanceNeeded = ComputeDistanceToAlignChosen(_stopIndex, _extraPasses);

        _state = State.Decelerating;
        _decelDuration = Mathf.Max(minDecelDuration, duration);
        _decelTotalDist = Mathf.Max(0f, distanceNeeded);
        _decelInitialSpeed = Mathf.Max(0f, _currSpeed);
        _decelT = 0f;
        _hermitePrevS = 0f;
    }

    void BuildStrip()
    {
        if (viewport == null || content == null || textItemPrefab == null) return;

        ClearActive();

        float vw = viewport.rect.width;
        float needWidth = vw * 1.75f;

        int sourceIndex = 0;
        float x = 0f;
        int safety = 0;

        var widthsOnce = new List<float>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
            widthsOnce.Add(MeasureWidth(entries[i]));

        while (x < needWidth && safety++ < 10000)
        {
            var item = Instantiate(textItemPrefab, content);
            string s = entries[sourceIndex];
            float w = widthsOnce[sourceIndex];

            item.SetText(s, w);
            var rt = item.Rect;
            rt.anchoredPosition = new Vector2(x, baselineY);

            _active.Add(item);
            _activeWidths.Add(w);

            x += w + spacing;
            sourceIndex = (sourceIndex + 1) % entries.Count;
        }
    }

    void ClearActive()
    {
        foreach (var it in _active) if (it) Destroy(it.gameObject);
        _active.Clear();
        _activeWidths.Clear();
    }

    float MeasureWidth(string s)
    {
        var tmp = textItemPrefab.tmp;
        Vector2 pref = tmp.GetPreferredValues(s);
        return Mathf.Max(10f, pref.x) + itemHorizontalPadding;
    }

    void RecycleItemsIfNeeded()
    {
        if (_active.Count == 0) return;

        float leftEdgeInContent = -_contentX;
        int guard = 0;

        while (guard++ < 50)
        {
            var first = _active[0];
            float firstRight = first.Rect.anchoredPosition.x + _activeWidths[0];

            if (firstRight < leftEdgeInContent)
            {
                _active.RemoveAt(0);
                _activeWidths.RemoveAt(0);

                int nextSourceIndex = NextSourceIndexAfterLast();
                string text = entries[nextSourceIndex];
                float width = MeasureWidth(text);

                first.SetText(text, width);

                float rightMost = RightEdgeXOfLast();
                first.Rect.anchoredPosition = new Vector2(rightMost + spacing, baselineY);

                _active.Add(first);
                _activeWidths.Add(width);
            }
            else break;
        }
    }

    float RightEdgeXOfLast()
    {
        if (_active.Count == 0) return 0f;
        var last = _active[_active.Count - 1];
        float w = _activeWidths[_activeWidths.Count - 1];
        return last.Rect.anchoredPosition.x + w;
    }

    int NextSourceIndexAfterLast()
    {
        if (_active.Count == 0 || entries.Count == 0) return 0;
        string lastText = _active[_active.Count - 1].CurrentText;
        int idx = entries.IndexOf(lastText);
        if (idx < 0) return 0;
        return (idx + 1) % entries.Count;
    }

    float ComputeDistanceToAlignChosen(int chosenIndex, int extraPasses)
    {
        float markerInContent = -_contentX + (viewport.rect.width * 0.5f) + markerOffsetX;

        int passesFound = -1;
        for (int i = 0; i < _active.Count; i++)
        {
            var it = _active[i];
            float w = _activeWidths[i];
            if (entries.IndexOf(it.CurrentText) == chosenIndex)
            {
                passesFound++;
                if (passesFound >= extraPasses)
                {
                    float centerX = it.Rect.anchoredPosition.x + w * 0.5f;
                    float delta = (centerX - markerInContent);
                    if (delta > 0f) return delta;
                }
            }
        }

        float tailRight = RightEdgeXOfLast();
        int idx = NextSourceIndexAfterLast();
        int maxSteps = entries.Count * (extraPasses + 2);

        for (int step = 0; step < maxSteps; step++)
        {
            string t = entries[idx];
            float w = MeasureWidth(t);
            float itemLeft = tailRight + spacing;
            float itemCenter = itemLeft + w * 0.5f;

            if (idx == chosenIndex)
            {
                passesFound++;
                if (passesFound >= extraPasses)
                {
                    float delta = (itemCenter - markerInContent);
                    if (delta > 0f) return delta;
                }
            }

            tailRight = itemLeft + w;
            idx = (idx + 1) % entries.Count;
        }

        return Mathf.Max(viewport.rect.width, spinSpeed * 0.6f);
    }

    void AlignChosenToMarker(int idxChosen)
    {
        float markerInContent = -_contentX + (viewport.rect.width * 0.5f) + markerOffsetX;

        RouletteTextItem best = null;
        float bestAbs = float.MaxValue;
        float bestCenter = 0f;

        foreach (var it in _active)
        {
            if (entries.IndexOf(it.CurrentText) != idxChosen) continue;
            float w = it.Width;
            float centerX = it.Rect.anchoredPosition.x + w * 0.5f;
            float d = Mathf.Abs(centerX - markerInContent);
            if (d < bestAbs)
            {
                bestAbs = d;
                best = it;
                bestCenter = centerX;
            }
        }

        if (best != null)
        {
            float delta = bestCenter - markerInContent;
            _contentX -= delta;
            content.anchoredPosition = new Vector2(_contentX, content.anchoredPosition.y);
            RecycleItemsIfNeeded();
        }
    }

    int Mod(int a, int m) => (a % m + m) % m;

    void OnDrawGizmosSelected()
    {
        if (!showCenterGuide || viewport == null) return;
        var vp = viewport;
        Vector3 worldCenter = vp.TransformPoint(new Vector3(vp.rect.width * 0.5f + markerOffsetX, 0f, 0f));
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(worldCenter + Vector3.up * 1000f, worldCenter + Vector3.down * 1000f);
    }
}
