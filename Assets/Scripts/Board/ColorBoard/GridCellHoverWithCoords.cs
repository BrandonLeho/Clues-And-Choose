using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Reflection;

[DisallowMultipleComponent]
public class GridCellHoverWithCoords : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Animation")]
    [SerializeField] float hoverScale = 1.08f;
    [SerializeField] float animSeconds = 0.18f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Text Fade + Content")]
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] bool deactivateLabelWhenHidden = true;
    [SerializeField] string coordinateFormat = "{C} {R}";
    [SerializeField, Range(0f, 1f)] float maxLabelAlpha = 1f;

    [Header("Grid Sizing (Auto unless overridden)")]
    [SerializeField] bool autoDetectGrid = true;
    [Min(1)] public int colsOverride = 30;
    [Min(1)] public int rowsOverride = 16;

    [Header("Orientation (A at top?)")]
    [SerializeField] bool aStartsAtTop = true;

    [Header("Optional Links")]
    [SerializeField] GridLayoutGroup grid;
    [SerializeField] MonoBehaviour boardLabelsComponent;
    [SerializeField] ScriptableObject paletteGridAsset;

    [Header("Label Highlighter")]
    [SerializeField] BoardLabelsHighlighter labelsHighlighter;

    [Header("Extra Graphics to Fade")]
    [SerializeField] Graphic[] extraGraphicsToFade;

    [Header("Bring To Front Options")]
    [SerializeField] RectTransform floatingLayer;
    [SerializeField] int hoverSortingOrder = 1000;

    [Header("Occupant Coin Lift")]
    [SerializeField] ValidDropSpot spot;
    [SerializeField] float coinLiftWorld = 0.12f;
    [SerializeField] AnimationCurve coinLiftEase;

    Vector3 _baseScale;
    Coroutine _anim;
    float _progress01;
    Color _labelBaseColor;
    Color[] _extraBaseColors;
    int _cols, _rows;
    bool _orientationAOnTop;
    int _lastColIdx = -1, _lastRowIdx = -1;

    RectTransform _rt;
    Transform _origParent;
    int _origSiblingIndex = -1;
    LayoutElement _placeholder;
    Canvas _tempCanvas;
    bool _isFloating;
    Image _img;
    bool _gridCached;

    Transform _occupantCoin;
    CoinPlacedLock _occupantLock;
    Vector3 _coinBaseWorld;
    bool _coinBaseCached;

    bool _isHovering;
    bool _liftEnabledForThisHover;

    Transform _homeParent;
    int _fixedGridIndex = -1;

    void Awake()
    {
        _rt = (RectTransform)transform;
        _baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;

        if (!labelsHighlighter) labelsHighlighter = GetComponentInParent<BoardLabelsHighlighter>(true);
        if (!grid) grid = GetComponentInParent<GridLayoutGroup>(true);
        if (!boardLabelsComponent) boardLabelsComponent = GetComponentInParent<MonoBehaviour>(true);

        _homeParent = transform.parent;

        _img = GetComponent<Image>();
        if (!_img)
        {
            _img = gameObject.AddComponent<Image>();
            _img.color = new Color(0, 0, 0, 0);
            _img.raycastTarget = true;
        }
        if (label)
        {
            _labelBaseColor = label.color;
            _labelBaseColor.a = maxLabelAlpha;
            SetLabelAlpha(0f);
            if (deactivateLabelWhenHidden) label.gameObject.SetActive(false);
        }
        if (extraGraphicsToFade != null && extraGraphicsToFade.Length > 0)
        {
            _extraBaseColors = new Color[extraGraphicsToFade.Length];
            for (int i = 0; i < extraGraphicsToFade.Length; i++)
                if (extraGraphicsToFade[i]) _extraBaseColors[i] = extraGraphicsToFade[i].color;
        }
        if (!spot) spot = GetComponent<ValidDropSpot>();
        CacheGridRefsOnce();
        ComputeFixedGridIndex();
    }

    void OnEnable()
    {
        transform.localScale = _baseScale;
        if (label && deactivateLabelWhenHidden) label.gameObject.SetActive(false);
        _progress01 = 0f;
        _isHovering = false;
        _liftEnabledForThisHover = false;

        if (_homeParent == null) _homeParent = transform.parent;
        if (_fixedGridIndex < 0) ComputeFixedGridIndex();
    }

    public void SetHoverEnabled(bool enabled) => this.enabled = enabled;

    void CacheGridRefsOnce()
    {
        if (_gridCached) return;
        _cols = Mathf.Max(1, colsOverride);
        _rows = Mathf.Max(1, rowsOverride);
        _orientationAOnTop = aStartsAtTop;
        if (autoDetectGrid)
        {
            if (grid)
            {
                if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0) _cols = grid.constraintCount;
                else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && grid.constraintCount > 0) _rows = grid.constraintCount;
            }
            if (!boardLabelsComponent) boardLabelsComponent = GetComponentInParent<MonoBehaviour>();
            if (boardLabelsComponent && boardLabelsComponent.GetType().Name == "BoardLabels")
            {
                var t = boardLabelsComponent.GetType();
                var fCols = t.GetField("cols", BindingFlags.Public | BindingFlags.Instance);
                var fRows = t.GetField("rows", BindingFlags.Public | BindingFlags.Instance);
                var fAAtTop = t.GetField("aStartsAtTop", BindingFlags.Public | BindingFlags.Instance);
                if (fCols != null) _cols = Mathf.Max(1, (int)fCols.GetValue(boardLabelsComponent));
                if (fRows != null) _rows = Mathf.Max(1, (int)fRows.GetValue(boardLabelsComponent));
                if (fAAtTop != null) _orientationAOnTop = (bool)fAAtTop.GetValue(boardLabelsComponent);
            }
            if (paletteGridAsset)
            {
                var t = paletteGridAsset.GetType();
                var fCols = t.GetField("cols", BindingFlags.Public | BindingFlags.Instance);
                var fRows = t.GetField("rows", BindingFlags.Public | BindingFlags.Instance);
                var fYTop = t.GetField("yZeroAtTop", BindingFlags.Public | BindingFlags.Instance);
                if (fCols != null) _cols = Mathf.Max(1, (int)fCols.GetValue(paletteGridAsset));
                if (fRows != null) _rows = Mathf.Max(1, (int)fRows.GetValue(paletteGridAsset));
                if (fYTop != null) _orientationAOnTop = (bool)fYTop.GetValue(paletteGridAsset);
            }
        }
        _gridCached = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IgnorePointerNow()) return;
        CacheGridRefsOnce();
        if (label) UpdateLabelToCoordsFast();

        BringToFront_Begin();
        StartAnim(1f);

        TryBindOccupant();
        _isHovering = true;
        _liftEnabledForThisHover = _occupantLock != null && _occupantLock.locked;
        if (_liftEnabledForThisHover) CacheCoinBaseIfNeeded();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (IgnorePointerNow()) return;
        _isHovering = false;
        StartAnim(0f);
        BringToFront_End();

        if (Mathf.Approximately(_progress01, 0f))
        {
            if (_occupantCoin)
            {
                var hover = _occupantCoin.GetComponentInChildren<CoinHoverOffset>();
                if (hover) hover.ResetOffset();
            }
            _coinBaseCached = false;
            _occupantCoin = null;
            _occupantLock = null;
        }
    }

    void BringToFront_Begin()
    {
        _origParent = _rt.parent;
        _origSiblingIndex = _rt.GetSiblingIndex();
        if (floatingLayer)
        {
            if (_placeholder == null)
            {
                var go = new GameObject("[Placeholder]", typeof(RectTransform), typeof(LayoutElement));
                var prt = go.GetComponent<RectTransform>();
                prt.SetParent(_origParent, false);
                prt.SetSiblingIndex(_origSiblingIndex);
                prt.anchorMin = _rt.anchorMin;
                prt.anchorMax = _rt.anchorMax;
                prt.pivot = _rt.pivot;
                prt.sizeDelta = _rt.sizeDelta;
                _placeholder = go.GetComponent<LayoutElement>();
                var size = _rt.rect.size;
                _placeholder.minWidth = _placeholder.preferredWidth = size.x;
                _placeholder.minHeight = _placeholder.preferredHeight = size.y;
            }
            else
            {
                _placeholder.transform.SetParent(_origParent, false);
                ((RectTransform)_placeholder.transform).SetSiblingIndex(_origSiblingIndex);
                _placeholder.ignoreLayout = false;
            }
            _isFloating = true;
            _rt.SetParent(floatingLayer, true);
            _rt.SetAsLastSibling();
        }
        else
        {
            if (!_tempCanvas) _tempCanvas = gameObject.AddComponent<Canvas>();
            _tempCanvas.overrideSorting = true;
            _tempCanvas.sortingOrder = hoverSortingOrder;
            if (!gameObject.GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    void BringToFront_End()
    {
        if (_isFloating)
        {
            _rt.SetParent(_origParent, true);
            if (_origSiblingIndex >= 0) _rt.SetSiblingIndex(_origSiblingIndex);
            if (_placeholder)
            {
                Destroy(_placeholder.gameObject);
                _placeholder = null;
            }
            _isFloating = false;
        }
        else if (_tempCanvas)
        {
            _tempCanvas.overrideSorting = false;
            _tempCanvas.sortingOrder = 0;
        }
    }

    void StartAnim(float target)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateTo(target));
    }

    IEnumerator AnimateTo(float target)
    {
        float start = _progress01;
        float time = 0f;
        if (target > start && label && deactivateLabelWhenHidden && !label.gameObject.activeSelf) label.gameObject.SetActive(true);
        while (time < animSeconds)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / animSeconds);
            _progress01 = Mathf.Lerp(start, target, ease.Evaluate(t));
            Apply(_progress01);
            yield return null;
        }
        _progress01 = target;
        Apply(_progress01);
        if (Mathf.Approximately(_progress01, 0f))
        {
            if (labelsHighlighter && _lastColIdx >= 0 && _lastRowIdx >= 0) labelsHighlighter.Clear();
            _lastColIdx = _lastRowIdx = -1;
            BringToFront_End();
            if (label && deactivateLabelWhenHidden) label.gameObject.SetActive(false);
        }
    }

    void Apply(float p)
    {
        float s = Mathf.Lerp(1f, hoverScale, p);
        transform.localScale = _baseScale * s;

        if (label) SetLabelAlpha(p);
        if (_extraBaseColors != null)
        {
            for (int i = 0; i < extraGraphicsToFade.Length; i++)
            {
                var g = extraGraphicsToFade[i];
                if (!g) continue;
                var c = _extraBaseColors[i];
                c.a = Mathf.Lerp(0f, _extraBaseColors[i].a, p);
                g.color = c;
            }
        }
        if (labelsHighlighter && _lastColIdx >= 0 && _lastRowIdx >= 0)
            labelsHighlighter.SetHighlightLerp(_lastColIdx, _lastRowIdx, _img ? _img.color : Color.white, p);

        if (!spot) spot = GetComponent<ValidDropSpot>();
        if (spot && spot.isOccupied && spot.occupant)
        {
            if (_occupantCoin == null || _occupantLock == null) TryBindOccupant();

            bool liftNow =
                _occupantCoin != null &&
                _occupantLock != null &&
                _occupantLock.locked &&
                (_isHovering || (_progress01 > 0f && _liftEnabledForThisHover));

            if (liftNow)
            {
                if (!_coinBaseCached)
                {
                    _coinBaseWorld = _occupantCoin.position;
                    _coinBaseCached = true;
                }

                var hover = _occupantCoin ? _occupantCoin.GetComponentInChildren<CoinHoverOffset>() : null;
                float liftWorld = Mathf.Lerp(0f, coinLiftWorld, _progress01);

                if (hover) hover.SetWorldLift(liftWorld);
                else _occupantCoin.position = _coinBaseWorld + Vector3.up * liftWorld;
            }
        }

        if (Mathf.Approximately(p, 0f)) ClearCoinCacheIfIdle();
    }

    void SetLabelAlpha(float a)
    {
        var c = _labelBaseColor;
        c.a = Mathf.Clamp01(a) * maxLabelAlpha;
        label.color = c;
    }

    void UpdateLabelToCoordsFast()
    {
        int idx = GetStableGridIndex();
        int col = idx % _cols;
        int row = idx / _cols;

        int letterIndex = _orientationAOnTop ? row : (_rows - 1 - row);
        letterIndex = Mathf.Clamp(letterIndex, 0, _rows - 1);

        string rowLetter = IndexToLetters(letterIndex);
        int colNumber = col + 1;
        if (label) label.text = coordinateFormat.Replace("{C}", colNumber.ToString()).Replace("{R}", rowLetter);

        _lastColIdx = col;
        _lastRowIdx = row;
    }

    static string IndexToLetters(int idx)
    {
        idx = Mathf.Max(0, idx);
        string s = "";
        while (idx >= 0)
        {
            int rem = idx % 26;
            s = (char)('A' + rem) + s;
            idx = idx / 26 - 1;
        }
        return s;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        hoverScale = Mathf.Max(1.0f, hoverScale);
        animSeconds = Mathf.Max(0.01f, animSeconds);
        if (!Application.isPlaying)
        {
            _progress01 = 0f;
            if (label)
            {
                _labelBaseColor = label.color;
                SetLabelAlpha(0f);
                if (deactivateLabelWhenHidden) label.gameObject.SetActive(false);
            }
            transform.localScale = _baseScale == Vector3.zero ? Vector3.one : _baseScale;
        }
    }
#endif

    void TryBindOccupant()
    {
        _occupantCoin = null;
        _occupantLock = null;
        if (spot && spot.isOccupied && spot.occupant)
        {
            _occupantCoin = spot.occupant.transform;
            _occupantLock = spot.occupant.GetComponent<CoinPlacedLock>();
        }
    }

    void CacheCoinBaseIfNeeded()
    {
        if (_occupantCoin == null || _occupantLock == null) return;
        if (!_occupantLock.locked) return;
        if (_coinBaseCached) return;
        _coinBaseWorld = _occupantCoin.position;
        _coinBaseCached = true;
    }

    void ClearCoinCacheIfIdle()
    {
        if (Mathf.Approximately(_progress01, 0f))
        {
            _coinBaseCached = false;
            _occupantCoin = null;
            _occupantLock = null;

            if (Mathf.Approximately(_progress01, 0f) && _occupantCoin)
            {
                var hover = _occupantCoin.GetComponentInChildren<CoinHoverOffset>();
                if (hover) hover.ResetOffset();
            }
        }
    }

    int GetStableGridIndex()
    {
        if (_fixedGridIndex >= 0) return _fixedGridIndex;
        ComputeFixedGridIndex();
        return Mathf.Max(0, _fixedGridIndex);
    }

    void ComputeFixedGridIndex()
    {
        if (_homeParent == null) _homeParent = transform.parent;
        if (_homeParent == null) { _fixedGridIndex = 0; return; }

        int count = 0, myIndex = 0;
        for (int i = 0; i < _homeParent.childCount; i++)
        {
            var child = _homeParent.GetChild(i);
            if (!child.GetComponent<GridCellHoverWithCoords>()) continue;
            if (child == transform) myIndex = count;
            count++;
        }
        _fixedGridIndex = myIndex;
    }

    public void ProbeEnter()
    {
        if (!_isHovering) OnPointerEnter(null);
    }

    public void ProbeExit()
    {
        if (_isHovering) OnPointerExit(null);
    }

    bool IgnorePointerNow()
    {
        return CoinPlacementProbe.ProbeMode;
    }
}
