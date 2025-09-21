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

    [Header("Dynamic Sizing")]
    [Range(0f, 0.5f)] public float paddingScale = 0.15f;
    [Range(0f, 0.5f)] public float spacingScale = 0.10f;


    [Header("Curved Track & Scale")]
    public bool enableCurvedTrack = true;
    public float curveHeight = 40f;
    public float baseTrackY = 0f;
    [Range(0.25f, 3f)] public float curveWidthFactor = 1.0f;
    [Range(0.5f, 3f)] public float curveExponent = 1.0f;
    [Range(0.1f, 1.0f)] public float scaleAtEdges = 0.85f;
    [Range(1.0f, 2.5f)] public float scaleAtCenter = 1.25f;
    public bool setCenterAsLastSibling = false;
    public bool curveEverywhere = true;
    public float minCurveH = -0.75f;

    [Header("Name Colors")]
    public bool useRegistryColors = true;
    public bool rebuildOnRegistryChanged = true;
    public bool preserveNameAlpha = true;
    public Color fallbackNameColor = Color.white;

    [Header("Center Highlight")]
    public bool enableCenterGlow = true;
    [Range(0f, 1f)] public float centerOutlineWidth = 0.25f;
    [Range(0f, 1f)] public float centerOutlineSoftness = 0.15f;
    public Color defaultOutlineColor = Color.white;
    public Color baseTextColor = Color.white;
    public bool useRegistryColorsForGlow = true;

    [Header("Center Glow")]
    [ColorUsage(true, true)] public Color glowOverrideColor = Color.white;
    [Range(-1f, 1f)] public float glowOffset = 0f;
    [Range(0f, 1f)] public float glowInner = 1f;
    [Range(0f, 1f)] public float glowOuter = 0.25f;
    [Range(0f, 1.5f)] public float glowPower = 0.75f;

    [Header("Winner Animation")]
    public bool enableWinnerAnimation = true;
    public float winnerDelay = 0.5f;
    public float winnerAnimDuration = 0.6f;
    public float othersDropDistance = 300f;
    public float winnerDropDistance = 60f;
    [Range(1f, 3f)] public float winnerScaleMultiplier = 1.4f;
    public AnimationCurve winnerPosCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve winnerScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool suspendCurveDuringWinnerAnim = true;


    readonly List<TMP_Text> _allLabels = new List<TMP_Text>();
    TMP_Text _highlighted;
    public UnityEvent<string, int> OnSpinComplete;

    RectTransform _viewport;
    RectTransform _content;

    readonly List<float> _itemCenters = new List<float>();

    float _listWidth;
    float _currentX;
    float _targetX;
    float _speed;
    float _decel;

    enum SpinState { Idle, Decelerating, Snapping }
    SpinState _state = SpinState.Idle;

    float _snapT;
    float _snapStartX;
    float _snapTargetX;

    bool _inWinnerAnim = false;

    struct LabelAnimState
    {
        public RectTransform rt;
        public Vector2 startPos;
        public Vector3 startScale;
        public Vector2 endPos;
        public Vector3 endScale;
    }
    readonly List<LabelAnimState> _winStates = new List<LabelAnimState>(128);


    System.Random _rng = new System.Random();

    void Awake()
    {
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
        switch (_state)
        {
            case SpinState.Decelerating:
                _speed = Mathf.Max(0f, _speed - _decel * dt);
                _currentX -= _speed * dt;
                while (_currentX <= -_listWidth) _currentX += _listWidth;
                _content.anchoredPosition = new Vector2(_currentX, 0f);
                UpdateCenterHighlight();
                UpdateCurvedTrackEffect();
                if (enableFinalSnap && _speed <= 30f) BeginFinalSnap();
                else if (!enableFinalSnap && _speed <= 0.0001f) SnapToExactTargetAndComplete();
                break;
            case SpinState.Snapping:
                _snapT += dt / Mathf.Max(0.0001f, snapDuration);
                float p = Mathf.Clamp01(_snapT);
                float eased = 1f - Mathf.Pow(1f - p, Mathf.Max(1f, snapEasePower));
                _currentX = Mathf.Lerp(_snapStartX, _snapTargetX, eased);
                _content.anchoredPosition = new Vector2(_currentX, 0f);
                UpdateCenterHighlight();
                UpdateCurvedTrackEffect();
                if (p >= 1f - 1e-5f) SnapToExactTargetAndComplete();
                break;
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

        int chosenIndex = forceTargetIndex >= 0 && forceTargetIndex < entries.Count ? forceTargetIndex : _rng.Next(0, entries.Count);
        Debug.Log(chosenIndex);

        float baseTargetUnwrapped = -(_itemCenters[chosenIndex] - ViewportCenterX());
        int loops = Mathf.Clamp(_rng.Next(minExtraLoops, maxExtraLoops + 1), 0, 100);

        _currentX = _content.anchoredPosition.x;
        _speed = Mathf.Max(10f, initialSpeed);

        float canonicalTarget = baseTargetUnwrapped - loops * _listWidth;
        float canonicalDistance = Mathf.Max(1f, _currentX - canonicalTarget);
        float scaledDecel = Mathf.Max(1e-3f, ((_speed * _speed) / (2f * canonicalDistance)) * Mathf.Max(0.01f, decelMultiplier));

        if (maxSlowdownSeconds > 0f)
        {
            float minRequiredDecel = _speed / Mathf.Max(0.01f, maxSlowdownSeconds);
            if (scaledDecel < minRequiredDecel) scaledDecel = minRequiredDecel;
        }

        float stopDistance = (_speed * _speed) / (2f * scaledDecel);
        float desiredStopX = _currentX - stopDistance;
        int k = Mathf.CeilToInt((baseTargetUnwrapped - desiredStopX) / _listWidth);
        _targetX = baseTargetUnwrapped - k * _listWidth;

        _decel = scaledDecel;
        _state = SpinState.Decelerating;
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        EnsureViewportAndContent();
        if (_content == null || _viewport == null) return;

        for (int i = _content.childCount - 1; i >= 0; i--)
            DestroyImmediate(_content.GetChild(i).gameObject);

        _allLabels.Clear();
        _itemCenters.Clear();
        _listWidth = 0f;

        if (entries == null || entries.Count == 0 || itemPrefab == null) return;

        var widths = new float[entries.Count];
        float maxWidth = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            TMP_Text measure = Instantiate(itemPrefab);
            measure.rectTransform.SetParent(transform, false);
            measure.text = entries[i];
            if (forceFontSize > 0f) measure.fontSize = forceFontSize;
            measure.ForceMeshUpdate();

            float w = measure.preferredWidth;
            if (w > maxWidth) maxWidth = w;
            DestroyImmediate(measure.gameObject);
        }

        float dynamicPadding = Mathf.Clamp(maxWidth * paddingScale, 16f, 150f);
        float dynamicSpacing = Mathf.Clamp(maxWidth * spacingScale, 8f, 115f);

        horizontalPadding = dynamicPadding;
        itemSpacing = dynamicSpacing;

        for (int i = 0; i < entries.Count; i++)
            widths[i] = maxWidth + horizontalPadding;

        float x = 0f;
        var centerItems = new List<RectTransform>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var t = Instantiate(itemPrefab, _content);
            InitLabelVisual(t);
            t.text = entries[i];
            t.raycastTarget = false;

            var rt = t.rectTransform;
            SetupRect(rt, widths[i], 0f);
            float half = widths[i] * 0.5f;
            x += half;
            rt.anchoredPosition = new Vector2(x, 0f);
            _itemCenters.Add(x);
            x += half + itemSpacing;
            centerItems.Add(rt);
            _allLabels.Add(t);
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

        foreach (var label in _allLabels)
        {
            ConfigureLabelForOuterMaskOnly(label);
        }

        UpdateCenterHighlight();
        UpdateCurvedTrackEffect();
    }

    void BuildCopy(List<RectTransform> sourceCenterItems, float offsetX)
    {
        for (int i = 0; i < sourceCenterItems.Count; i++)
        {
            var src = sourceCenterItems[i];
            var t = Instantiate(itemPrefab, _content);
            InitLabelVisual(t);
            t.text = entries[i];
            t.raycastTarget = false;

            var rt = t.rectTransform;
            SetupRect(rt, src.sizeDelta.x, 0f);
            rt.anchoredPosition = new Vector2(src.anchoredPosition.x + offsetX, 0f);
            _allLabels.Add(t);
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
            _content.sizeDelta = Vector2.zero;
        }

        EnforceSingleViewportMask(_viewport);
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
        float m = _listWidth;
        float wrapped = Mathf.Repeat((_targetX - _currentX) + m * 0.5f, m) - m * 0.5f;
        _snapTargetX = _currentX + wrapped;
    }

    void SnapToExactTargetAndComplete()
    {
        _currentX = _snapTargetX;
        if (_state != SpinState.Snapping)
        {
            float m = _listWidth;
            float wrapped = Mathf.Repeat((_targetX - _currentX) + m * 0.5f, m) - m * 0.5f;
            _currentX += wrapped;
        }
        _content.anchoredPosition = new Vector2(_currentX, 0f);
        UpdateCurvedTrackEffect();
        _state = SpinState.Idle;
        if (enableWinnerAnimation)
            StartCoroutine(CoPlayWinnerSequenceWithDelay());

        int idx = Mathf.Clamp(GetIndexForTargetX(_targetX), 0, entries.Count - 1);
        OnSpinComplete?.Invoke(entries[idx], idx);
        Debug.Log("Winner: " + entries[idx]);
    }

    bool TryResolveRegistryColor(string ownerName, out Color color)
    {
        color = Color.white;
        if (!useRegistryColorsForGlow || string.IsNullOrWhiteSpace(ownerName)) return false;
        var reg = ColorLockRegistry.GetOrFind();
        if (!reg) return false;

        foreach (var kv in reg.lockedBy)
        {
            int index = kv.Key;
            uint netId = kv.Value;
            if (reg.labelByIndex.TryGetValue(index, out var label) && string.Equals(label, ownerName, StringComparison.Ordinal))
            {
                if (reg.colorByOwner.TryGetValue(netId, out var c32))
                {
                    color = c32;
                    return true;
                }
            }
        }
        return false;
    }

    void TryHookRegistryEvents(bool hook)
    {
        if (!useRegistryColorsForGlow) return;
        var reg = ColorLockRegistry.GetOrFind();
        if (!reg) return;
        if (hook) reg.OnRegistryChanged += OnRegistryChanged;
        else reg.OnRegistryChanged -= OnRegistryChanged;
    }

    void OnEnable()
    {
        TryHookRegistryEvents(true);
        OnSpinComplete.RemoveListener(OnSpinDonePlayWinAnim);
        OnSpinComplete.AddListener(OnSpinDonePlayWinAnim);
    }
    void OnDisable()
    {
        TryHookRegistryEvents(false);
        OnSpinComplete.RemoveListener(OnSpinDonePlayWinAnim);
    }

    void SetOutline(TMP_Text t, Color col, float width, float softness)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, softness);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, col);
        t.fontMaterial = mat;
    }

    void ClearOutline(TMP_Text t)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        t.fontMaterial = mat;
    }

    TMP_Text FindLabelAtViewportCenter()
    {
        if (_allLabels.Count == 0 || _viewport == null) return null;
        float centerXInContent = ViewportCenterX() - _currentX;
        TMP_Text best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _allLabels.Count; i++)
        {
            var t = _allLabels[i];
            if (!t) continue;
            float dx = Mathf.Abs(t.rectTransform.anchoredPosition.x - centerXInContent);
            if (dx < bestDist)
            {
                bestDist = dx;
                best = t;
            }
        }
        return best;
    }

    void UpdateCenterHighlight()
    {
        if (!enableCenterGlow || _inWinnerAnim) return;
        var now = FindLabelAtViewportCenter();
        if (now == _highlighted && now != null)
        {
            ApplyHighlightColor(now);
            return;
        }
        if (_highlighted)
        {
            ClearOutline(_highlighted);
            ClearGlow(_highlighted);
        }
        _highlighted = now;
        ApplyHighlightColor(_highlighted);
    }

    void ApplyHighlightColor(TMP_Text t)
    {
        if (!t) return;

        Color col;
        if (useRegistryColorsForGlow && TryResolveRegistryColor(t.text, out var regCol))
            col = regCol;
        else
            col = glowOverrideColor;

        SetOutline(t, col, centerOutlineWidth, centerOutlineSoftness);

        if (enableCenterGlow)
            SetGlow(t, col, glowOffset, glowInner, glowOuter, glowPower);
        else
            ClearGlow(t);
    }


    void OnRegistryChanged() => ApplyHighlightColor(_highlighted);

    static void SetupRect(RectTransform rt, float width, float y)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(0f, y);
    }

    void SetGlow(TMP_Text t, Color col, float offset, float inner, float outer, float power)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
        mat.SetColor(ShaderUtilities.ID_GlowColor, col);
        mat.SetFloat(ShaderUtilities.ID_GlowOffset, offset);
        mat.SetFloat(ShaderUtilities.ID_GlowInner, inner);
        mat.SetFloat(ShaderUtilities.ID_GlowOuter, outer);
        mat.SetFloat(ShaderUtilities.ID_GlowPower, power);
        t.fontMaterial = mat;
    }

    void ClearGlow(TMP_Text t)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.DisableKeyword(ShaderUtilities.Keyword_Glow);
        mat.SetFloat(ShaderUtilities.ID_GlowInner, 0f);
        mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0f);
        mat.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
        t.fontMaterial = mat;
    }

    void InitLabelVisual(TMP_Text t)
    {
        if (!t) return;
        if (forceFontSize > 0f) t.fontSize = forceFontSize;

        if (forceColor.a > 0f) t.color = forceColor;
        else t.color = baseTextColor;

        ClearOutline(t);
        ClearGlow(t);
    }


    void UpdateCurvedTrackEffect()
    {
        if (!enableCurvedTrack || _allLabels == null || _allLabels.Count == 0 || _viewport == null || _inWinnerAnim)
            return;

        float centerXInContent = ViewportCenterX() - _currentX;

        float radius = (_viewport.rect.width * 0.5f) * curveWidthFactor;
        if (radius <= 1e-3f) radius = 1e-3f;

        TMP_Text closest = null;
        float bestDx = float.MaxValue;

        for (int i = 0; i < _allLabels.Count; i++)
        {
            var t = _allLabels[i];
            if (!t) continue;

            var rt = t.rectTransform;

            float dx = rt.anchoredPosition.x - centerXInContent;
            float ax = Mathf.Abs(dx);

            float h;

            if (curveEverywhere)
            {
                float u = ax / radius;
                h = 1f - Mathf.Pow(u, 2f * Mathf.Max(0.0001f, curveExponent));

                if (minCurveH < 0f) h = Mathf.Max(minCurveH, h);
            }
            else
            {
                float u = Mathf.Clamp01(ax / radius);
                float uExp = Mathf.Pow(u, curveExponent);
                h = 1f - (uExp * uExp);
            }

            float y = baseTrackY + (curveHeight * h);

            float s = Mathf.LerpUnclamped(scaleAtEdges, scaleAtCenter, h);

            var ap = rt.anchoredPosition;
            ap.y = y;
            rt.anchoredPosition = ap;
            rt.localScale = new Vector3(s, s, 1f);

            if (ax < bestDx)
            {
                bestDx = ax;
                closest = t;
            }
        }

        if (setCenterAsLastSibling && closest)
            closest.transform.SetAsLastSibling();
    }


    System.Collections.IEnumerator CoPlayWinnerSequenceWithDelay()
    {
        if (!enableWinnerAnimation) yield break;
        if (_inWinnerAnim) yield break;

        var winner = _highlighted != null ? _highlighted : FindLabelAtViewportCenter();
        if (winner == null) yield break;

        _inWinnerAnim = true;

        float delay = Mathf.Max(0f, winnerDelay);
        if (delay > 0f) yield return new WaitForSeconds(delay);

        _winStates.Clear();
        var wrt = winner.rectTransform;

        for (int i = 0; i < _allLabels.Count; i++)
        {
            var t = _allLabels[i];
            if (!t) continue;

            var rt = t.rectTransform;
            var s = new LabelAnimState
            {
                rt = rt,
                startPos = rt.anchoredPosition,
                startScale = rt.localScale
            };

            if (t == winner)
            {
                s.endPos = s.startPos + new Vector2(0f, -winnerDropDistance);
                s.endScale = s.startScale * winnerScaleMultiplier;
            }
            else
            {
                s.endPos = s.startPos + new Vector2(0f, -othersDropDistance);
                s.endScale = s.startScale;
            }

            _winStates.Add(s);
        }

        winner.transform.SetAsLastSibling();

        float dur = Mathf.Max(0.001f, winnerAnimDuration);
        float tNorm = 0f;
        while (tNorm < 1f)
        {
            tNorm += Time.deltaTime / dur;
            float pPos = winnerPosCurve.Evaluate(Mathf.Clamp01(tNorm));
            float pScl = winnerScaleCurve.Evaluate(Mathf.Clamp01(tNorm));

            for (int i = 0; i < _winStates.Count; i++)
            {
                var st = _winStates[i];
                if (!st.rt) continue;

                Vector2 pos = Vector2.LerpUnclamped(st.startPos, st.endPos, pPos);

                Vector3 scl = (st.rt == wrt)
                    ? Vector3.LerpUnclamped(st.startScale, st.endScale, pScl)
                    : st.startScale;

                st.rt.anchoredPosition = pos;
                st.rt.localScale = scl;
            }

            yield return null;
        }

        for (int i = 0; i < _winStates.Count; i++)
        {
            var st = _winStates[i];
            if (!st.rt) continue;
            st.rt.anchoredPosition = st.endPos;
            st.rt.localScale = st.endScale;
        }

        _inWinnerAnim = false;
    }

    void OnSpinDonePlayWinAnim(string name, int index)
    {
        if (enableWinnerAnimation)
            StartCoroutine(CoPlayWinnerSequenceWithDelay());
    }

    void EnforceSingleViewportMask(RectTransform viewport)
    {
        foreach (var m in viewport.GetComponentsInChildren<RectMask2D>(true))
            if (m.gameObject != viewport.gameObject)
                Destroy(m);

        foreach (var m in viewport.GetComponentsInChildren<Mask>(true))
            if (m.gameObject != viewport.gameObject)
                Destroy(m);
    }

    void ConfigureLabelForOuterMaskOnly(TMP_Text t)
    {
        if (!t) return;
        t.maskable = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
    }
}
