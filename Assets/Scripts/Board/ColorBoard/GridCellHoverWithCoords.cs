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
    [Tooltip("Format for coordinate text. {C}=column number, {R}=row letter.")]
    [SerializeField] string coordinateFormat = "{C} {R}"; // e.g., "12 B" (column number then row letter)
    [SerializeField, Range(0f, 1f)] float maxLabelAlpha = 1f;

    [Header("Grid Sizing (Auto unless overridden)")]
    [Tooltip("Auto-read cols/rows from GridLayoutGroup/BoardLabels/PaletteGrid when possible.")]
    [SerializeField] bool autoDetectGrid = true;
    [Min(1)] public int colsOverride = 30;
    [Min(1)] public int rowsOverride = 16;

    [Header("Orientation (A at top?)")]
    [Tooltip("If auto-detect is on, we try BoardLabels.aStartsAtTop or PaletteGrid.yZeroAtTop. Otherwise use this.")]
    [SerializeField] bool aStartsAtTop = true;

    [Header("Optional Links")]
    [SerializeField] GridLayoutGroup grid;                 // parent grid (auto if null)
    [SerializeField] MonoBehaviour boardLabelsComponent;    // optional: assign your BoardLabels
    [SerializeField] ScriptableObject paletteGridAsset;     // optional: assign your PaletteGrid

    [Header("Label Highlighter")]
    [SerializeField] BoardLabelsHighlighter labelsHighlighter;

    [Header("Extra Graphics to Fade")]
    [SerializeField] Graphic[] extraGraphicsToFade;

    // runtime
    Vector3 _baseScale;
    Coroutine _anim;
    float _progress01; // 0..1
    Color _labelBaseColor;
    Color[] _extraBaseColors;
    int _cols, _rows;
    bool _orientationAOnTop;
    int _lastColIdx = -1, _lastRowIdx = -1;

    void Awake()
    {
        _baseScale = transform.localScale;

        CacheGridRefs();

        if (label != null)
        {
            // Cache the base RGB, but force alpha to maxLabelAlpha
            _labelBaseColor = label.color;
            _labelBaseColor.a = maxLabelAlpha;

            // Start hidden
            SetLabelAlpha(0f);
            if (deactivateLabelWhenHidden) label.gameObject.SetActive(false);
        }

        if (extraGraphicsToFade != null && extraGraphicsToFade.Length > 0)
        {
            _extraBaseColors = new Color[extraGraphicsToFade.Length];
            for (int i = 0; i < extraGraphicsToFade.Length; i++)
            {
                if (extraGraphicsToFade[i] != null)
                    _extraBaseColors[i] = extraGraphicsToFade[i].color;
            }
        }

        // Ensure we can receive raycasts
        var g = GetComponent<Graphic>();
        if (g == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = true;
        }

        if (!labelsHighlighter)
            labelsHighlighter = GetComponentInParent<BoardLabelsHighlighter>();

        if (!grid) grid = GetComponentInParent<GridLayoutGroup>(true);
        if (!labelsHighlighter) labelsHighlighter = GetComponentInParent<BoardLabelsHighlighter>(true);

        if (!boardLabelsComponent)
            boardLabelsComponent = GetComponentInParent<MonoBehaviour>(true); // your type check remains

    }

    void CacheGridRefs()
    {
        if (!grid) grid = GetComponentInParent<GridLayoutGroup>();

        // Defaults
        _cols = Mathf.Max(1, colsOverride);
        _rows = Mathf.Max(1, rowsOverride);
        _orientationAOnTop = aStartsAtTop;

        if (!autoDetectGrid) return;

        // 1) Try GridLayoutGroup constraint
        if (grid)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0)
                _cols = grid.constraintCount;
            else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && grid.constraintCount > 0)
                _rows = grid.constraintCount;
        }

        // 2) Try BoardLabels on this or parent (public fields: cols/rows/aStartsAtTop)
        if (!boardLabelsComponent)
            boardLabelsComponent = GetComponentInParent<MonoBehaviour>(); // harmless; we verify by type name
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

        // 3) Try PaletteGrid asset (public fields: cols/rows/yZeroAtTop)
        if (paletteGridAsset)
        {
            var t = paletteGridAsset.GetType();
            var fCols = t.GetField("cols", BindingFlags.Public | BindingFlags.Instance);
            var fRows = t.GetField("rows", BindingFlags.Public | BindingFlags.Instance);
            var fYTop = t.GetField("yZeroAtTop", BindingFlags.Public | BindingFlags.Instance);
            if (fCols != null) _cols = Mathf.Max(1, (int)fCols.GetValue(paletteGridAsset));
            if (fRows != null) _rows = Mathf.Max(1, (int)fRows.GetValue(paletteGridAsset));
            if (fYTop != null) _orientationAOnTop = (bool)fYTop.GetValue(paletteGridAsset); // true => A at top
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        UpdateLabelToCoords(); // already sets label text

        // NEW: compute indices and call highlighter
        int idx = transform.GetSiblingIndex();
        int col = (_cols <= 0) ? 0 : (idx % _cols);   // 0-based, left->right
        int row = (_cols <= 0) ? 0 : (idx / _cols);   // 0-based, top->bottom

        _lastColIdx = col; _lastRowIdx = row;

        if (labelsHighlighter)
        {
            // Get the cell's current color (from its Image background)
            var img = GetComponent<Image>();
            Color c = img ? img.color : Color.white;

            labelsHighlighter.Highlight(col, row, c);
        }

        StartAnim(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (labelsHighlighter) labelsHighlighter.Clear();
        _lastColIdx = _lastRowIdx = -1;

        StartAnim(0f);
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

        if (target > start && label && deactivateLabelWhenHidden && !label.gameObject.activeSelf)
            label.gameObject.SetActive(true);

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

        if (Mathf.Approximately(_progress01, 0f) && label && deactivateLabelWhenHidden)
            label.gameObject.SetActive(false);

        _anim = null;
    }

    void Apply(float p)
    {
        // Scale the cell
        float s = Mathf.Lerp(1f, hoverScale, p);
        transform.localScale = _baseScale * s;

        // Fade the cell's own label
        if (label) SetLabelAlpha(p);

        // Fade any extra graphics
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
        {
            var img = GetComponent<Image>();
            Color c = img ? img.color : Color.white;
            labelsHighlighter.SetHighlightLerp(_lastColIdx, _lastRowIdx, c, p);
        }
    }


    void SetLabelAlpha(float a)
    {
        var c = _labelBaseColor;
        c.a = Mathf.Clamp01(a) * maxLabelAlpha;
        label.color = c;
    }

    void UpdateLabelToCoords()
    {
        if (!label) return;

        // Re-cache in case layout changed since Awake
        CacheGridRefs();

        int idx = transform.GetSiblingIndex();
        if (_cols <= 0) _cols = 1;

        int col = (idx % _cols);         // 0-based
        int row = (idx / _cols);         // 0-based from top if parent orders top->bottom

        // If A is at top, letter = row; if A is at bottom, flip
        int letterIndex = _orientationAOnTop ? row : (_rows - 1 - row);
        letterIndex = Mathf.Clamp(letterIndex, 0, Mathf.Max(0, _rows - 1));

        string rowLetter = IndexToLetters(letterIndex); // A, B, ... P
        int colNumber = col + 1;

        label.text = coordinateFormat
            .Replace("{C}", colNumber.ToString())
            .Replace("{R}", rowLetter);
    }

    // A, B, ..., Z, AA, AB, ...
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
}
