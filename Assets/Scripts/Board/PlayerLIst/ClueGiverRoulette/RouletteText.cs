using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class RouletteText : MonoBehaviour
{
    public List<string> entries = new List<string>();

    public TMP_Text itemPrefab;
    public float horizontalPadding = 32f;
    public float itemSpacing = 24f;
    public float forceFontSize = 0f;
    public Color forceColor = new Color(0, 0, 0, 0);

    public float initialSpeed = 1200f;
    public float decelMultiplier = 1.0f;
    public int minExtraLoops = 2;
    public int maxExtraLoops = 4;
    public int forceTargetIndex = -1;
    public float maxSlowdownSeconds = 0f;

    public bool enableFinalSnap = true;
    public float snapDuration = 0.25f;
    public float snapEasePower = 2.0f;

    public int copiesEachSide = 3;

    public UnityEvent<string, int> OnSpinComplete;

    RectTransform _root;
    RectTransform _viewport;
    RectTransform _content;

    readonly List<float> _itemCenters = new List<float>();

    float _listWidth;
    float _currentX;
    float _targetX;
    float _speed;
    float _decel;
    bool _spinning;

    enum SpinState { Idle, Decelerating, Snapping }
    SpinState _state = SpinState.Idle;

    float _snapT;
    float _snapStartX;
    float _snapTargetX;

    System.Random _rng = new System.Random();

    void Awake()
    {
        _root = (RectTransform)transform;
        EnsureViewportAndContent();
    }

    void Start()
    {
        Rebuild();
    }

    void Update()
    {
        if (_state == SpinState.Idle) return;

        float dt = Time.unscaledDeltaTime;

        if (_state == SpinState.Decelerating)
        {
            _speed = Mathf.Max(0f, _speed - _decel * dt);
            _currentX -= _speed * dt;

            while (_currentX <= -_listWidth) _currentX += _listWidth;

            _content.anchoredPosition = new Vector2(_currentX, 0f);

            if (enableFinalSnap && _speed <= 30f)
            {
                BeginFinalSnap();
            }
            else if (!enableFinalSnap && _speed <= 0.0001f)
            {
                SnapToExactTargetAndComplete();
            }
        }
        else if (_state == SpinState.Snapping)
        {
            _snapT += dt / Mathf.Max(0.0001f, snapDuration);
            float p = Mathf.Clamp01(_snapT);
            float eased = 1f - Mathf.Pow(1f - p, Mathf.Max(1f, snapEasePower));

            _currentX = Mathf.Lerp(_snapStartX, _snapTargetX, eased);
            _content.anchoredPosition = new Vector2(_currentX, 0f);

            if (p >= 1f - 1e-5f)
            {
                SnapToExactTargetAndComplete();
            }
        }
    }

    public void StartSpin()
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[TextRoulette2D] No entries to spin.");
            return;
        }
        if (_listWidth <= 0f || _itemCenters.Count != entries.Count)
        {
            Rebuild();
            if (_listWidth <= 0f) return;
        }

        int chosenIndex = forceTargetIndex >= 0 && forceTargetIndex < entries.Count
            ? forceTargetIndex
            : _rng.Next(0, entries.Count);

        float baseTargetUnwrapped = -(_itemCenters[chosenIndex] - ViewportCenterX());
        int loops = Mathf.Clamp(_rng.Next(minExtraLoops, maxExtraLoops + 1), 0, 100);

        _currentX = _content.anchoredPosition.x;
        _speed = Mathf.Max(10f, initialSpeed);

        float canonicalTarget = baseTargetUnwrapped - loops * _listWidth;
        float canonicalDistance = Mathf.Max(1f, _currentX - canonicalTarget);
        float baseDecel = (_speed * _speed) / (2f * canonicalDistance);
        float scaledDecel = Mathf.Max(1e-3f, baseDecel * Mathf.Max(0.01f, decelMultiplier));
        float stopDistance = (_speed * _speed) / (2f * scaledDecel);
        float desiredStopX = _currentX - stopDistance;

        int k = Mathf.CeilToInt((baseTargetUnwrapped - desiredStopX) / _listWidth);
        _targetX = baseTargetUnwrapped - k * _listWidth;

        if (maxSlowdownSeconds > 0f)
        {
            float minRequiredDecel = _speed / Mathf.Max(0.01f, maxSlowdownSeconds);
            scaledDecel = Mathf.Max(scaledDecel, minRequiredDecel);
            stopDistance = (_speed * _speed) / (2f * scaledDecel);
            desiredStopX = _currentX - stopDistance;
            k = Mathf.CeilToInt((baseTargetUnwrapped - desiredStopX) / _listWidth);
            _targetX = baseTargetUnwrapped - k * _listWidth;
            _decel = scaledDecel;
        }

        _decel = scaledDecel;
        _spinning = true;
        _state = SpinState.Decelerating;
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        for (int i = _content.childCount - 1; i >= 0; i--)
            DestroyImmediate(_content.GetChild(i).gameObject);

        _itemCenters.Clear();
        _listWidth = 0f;

        if (entries == null || entries.Count == 0 || itemPrefab == null)
            return;

        var widths = new float[entries.Count];
        float maxWidth = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            TMP_Text measure = Instantiate(itemPrefab);
            measure.rectTransform.SetParent(transform, false);
            measure.text = entries[i];
            if (forceFontSize > 0f) measure.fontSize = forceFontSize;
            measure.ForceMeshUpdate();
            float w = measure.preferredWidth + horizontalPadding;
            maxWidth = Mathf.Max(maxWidth, w);
            DestroyImmediate(measure.gameObject);
        }

        for (int i = 0; i < entries.Count; i++)
            widths[i] = maxWidth;

        float x = 0f;
        var centerItems = new List<RectTransform>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var t = Instantiate(itemPrefab, _content);
            if (forceFontSize > 0f) t.fontSize = forceFontSize;
            if (forceColor.a > 0f) t.color = forceColor;
            t.text = entries[i];
            t.raycastTarget = false;

            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(widths[i], rt.sizeDelta.y);

            float half = widths[i] * 0.5f;
            x += half;
            rt.anchoredPosition = new Vector2(x, 0f);
            _itemCenters.Add(x);
            x += half + itemSpacing;

            centerItems.Add(rt);
        }
        _listWidth = x - itemSpacing;

        int sideCopies = Mathf.Max(1, copiesEachSide);
        for (int c = 1; c <= sideCopies; c++)
        {
            float leftOffset = -_listWidth * c;
            float rightOffset = _listWidth * c;
            BuildCopy(centerItems, leftOffset);
            BuildCopy(centerItems, rightOffset);
        }

        _currentX = 0f;
        _content.anchoredPosition = new Vector2(_currentX, 0f);
    }

    void BuildCopy(List<RectTransform> sourceCenterItems, float offsetX)
    {
        for (int i = 0; i < sourceCenterItems.Count; i++)
        {
            var src = sourceCenterItems[i];
            var t = Instantiate(itemPrefab, _content);
            if (forceFontSize > 0f) t.fontSize = forceFontSize;
            if (forceColor.a > 0f) t.color = forceColor;
            t.text = entries[i];
            t.raycastTarget = false;

            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(src.sizeDelta.x, src.sizeDelta.y);
            rt.anchoredPosition = new Vector2(src.anchoredPosition.x + offsetX, 0f);
        }
    }

    void EnsureViewportAndContent()
    {
        _viewport = transform.Find("Viewport") as RectTransform;
        if (_viewport == null)
        {
            var go = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            _viewport = go.GetComponent<RectTransform>();
            _viewport.SetParent(transform, false);
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.pivot = new Vector2(0.5f, 0.5f);
            _viewport.offsetMin = Vector2.zero;
            _viewport.offsetMax = Vector2.zero;
            _viewport.SetAsLastSibling();
        }

        _content = _viewport.Find("Content") as RectTransform;
        if (_content == null)
        {
            var go = new GameObject("Content", typeof(RectTransform));
            _content = go.GetComponent<RectTransform>();
            _content.SetParent(_viewport, false);
            _content.anchorMin = new Vector2(0f, 0.5f);
            _content.anchorMax = new Vector2(0f, 0.5f);
            _content.pivot = new Vector2(0f, 0.5f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);
        }
    }

    float ViewportCenterX()
    {
        return _viewport.rect.width * 0.5f;
    }

    int GetIndexForTargetX(float targetX)
    {
        float wantCenter = ViewportCenterX() - targetX;
        float wc = wantCenter % _listWidth;
        if (wc < 0) wc += _listWidth;

        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _itemCenters.Count; i++)
        {
            float d = Mathf.Abs(_itemCenters[i] - wc);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    void BeginFinalSnap()
    {
        _state = SpinState.Snapping;
        _snapT = 0f;
        _snapStartX = _currentX;
        float delta = _targetX - _currentX;
        float m = _listWidth;
        float wrapped = Mathf.Repeat(delta + m * 0.5f, m) - m * 0.5f;
        _snapTargetX = _currentX + wrapped;
    }

    void SnapToExactTargetAndComplete()
    {
        _currentX = _snapTargetX;
        if (_state != SpinState.Snapping)
        {
            float m = _listWidth;
            float delta = _targetX - _currentX;
            float wrapped = Mathf.Repeat(delta + m * 0.5f, m) - m * 0.5f;
            _currentX += wrapped;
        }

        _content.anchoredPosition = new Vector2(_currentX, 0f);
        _state = SpinState.Idle;
        _spinning = false;

        int idx = GetIndexForTargetX(_targetX);
        idx = Mathf.Clamp(idx, 0, entries.Count - 1);
        OnSpinComplete?.Invoke(entries[idx], idx);
    }
}
