using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class BoardLabelsHighlighter : MonoBehaviour
{
    [Header("References")]
    public MonoBehaviour boardLabels;

    [Header("Outline Style")]
    [Range(0f, 1f)] public float highlightOutlineWidth = 0.25f;
    [Range(0f, 1f)] public float idleOutlineWidth = 0f;
    public bool tintFaceOnHighlight = false;
    public Color faceHighlightTint = Color.white;

    [Header("Outline Fade Settings")]
    [SerializeField, Range(0f, 1f)] float maxOutlineWidth = 0.25f;
    [SerializeField] bool fadeOutlineAlpha = true;

    [Header("Label Discovery (advanced)")]
    public bool autoFindByNames = true;

    TextMeshProUGUI[] _top, _bottom, _left, _right;
    Material[] _topMats, _bottomMats, _leftMats, _rightMats;
    int _lastCol = -1, _lastRow = -1;

    static readonly int ID_OutlineWidth = Shader.PropertyToID("_OutlineWidth");
    static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
    static readonly int ID_FaceColor = Shader.PropertyToID("_FaceColor");

    public float maxScale = 1.2f;
    Vector3 _baseScale = Vector3.one;

    void Awake()
    {
        if (!boardLabels)
        {
            boardLabels = GetComponentsInParent<MonoBehaviour>(true)
                .Concat(GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(m => m && m.GetType().Name == "BoardLabels");
        }
        BuildCaches();
    }

    void OnDestroy()
    {
        if (_topMats != null) for (int i = 0; i < _topMats.Length; i++) if (_topMats[i]) Destroy(_topMats[i]);
        if (_bottomMats != null) for (int i = 0; i < _bottomMats.Length; i++) if (_bottomMats[i]) Destroy(_bottomMats[i]);
        if (_leftMats != null) for (int i = 0; i < _leftMats.Length; i++) if (_leftMats[i]) Destroy(_leftMats[i]);
        if (_rightMats != null) for (int i = 0; i < _rightMats.Length; i++) if (_rightMats[i]) Destroy(_rightMats[i]);
    }

    void BuildCaches()
    {
        var root = boardLabels ? ((Component)boardLabels).transform : transform;
        Transform topT = null, bottomT = null, leftT = null, rightT = null;
        if (autoFindByNames)
        {
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("top") && topT == null) topT = t;
                else if (n.Contains("bottom") && bottomT == null) bottomT = t;
                else if (n.Contains("left") && leftT == null) leftT = t;
                else if (n.Contains("right") && rightT == null) rightT = t;
            }
        }
        if (!topT || !bottomT || !leftT || !rightT)
        {
            var allParents = root.GetComponentsInChildren<RectTransform>(true);
            var groups = new List<(Transform tr, int count)>();
            foreach (var p in allParents)
            {
                int cnt = p.GetComponentsInChildren<TextMeshProUGUI>(true).Length;
                if (cnt > 0) groups.Add((p, cnt));
            }
            foreach (var g in groups.OrderByDescending(g => g.count).Take(10))
            {
                if (!topT) topT = g.tr;
                else if (!bottomT && g.tr != topT) bottomT = g.tr;
                else if (!leftT && g.tr != topT && g.tr != bottomT) leftT = g.tr;
                else if (!rightT && g.tr != topT && g.tr != bottomT && g.tr != leftT) rightT = g.tr;
            }
        }
        _top = GetSortedChildren(topT, true);
        _bottom = GetSortedChildren(bottomT, true);
        _left = GetSortedChildren(leftT, false);
        _right = GetSortedChildren(rightT, false);
        _topMats = CreateMaterialClones(_top);
        _bottomMats = CreateMaterialClones(_bottom);
        _leftMats = CreateMaterialClones(_left);
        _rightMats = CreateMaterialClones(_right);
        ClearAll();
    }

    static TextMeshProUGUI[] GetSortedChildren(Transform parent, bool horizontal)
    {
        if (!parent) return new TextMeshProUGUI[0];
        var arr = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
        return arr.OrderBy(t => { var rt = t.transform as RectTransform; return horizontal ? rt.anchoredPosition.x : -rt.anchoredPosition.y; }).ToArray();
    }

    static Material[] CreateMaterialClones(TextMeshProUGUI[] labels)
    {
        var mats = new Material[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            if (!labels[i]) continue;
            mats[i] = new Material(labels[i].fontMaterial);
            labels[i].fontMaterial = mats[i];
        }
        return mats;
    }

    public void SetHighlightLerp(int colIndex, int rowIndex, Color targetColor, float progress)
    {
        if (colIndex != _lastCol || rowIndex != _lastRow)
        {
            Clear();
            _lastCol = colIndex;
            _lastRow = rowIndex;
        }
        float width = Mathf.Lerp(idleOutlineWidth, maxOutlineWidth, progress);
        Color outline = targetColor;
        if (fadeOutlineAlpha) outline.a *= progress;
        Vector3 scale = Vector3.Lerp(_baseScale, Vector3.one * maxScale, progress);
        if (colIndex >= 0)
        {
            if (colIndex < _top.Length) ApplyFadeAndScale(_top[colIndex], _topMats[colIndex], width, outline, scale);
            if (colIndex < _bottom.Length) ApplyFadeAndScale(_bottom[colIndex], _bottomMats[colIndex], width, outline, scale);
        }
        if (rowIndex >= 0)
        {
            if (rowIndex < _left.Length) ApplyFadeAndScale(_left[rowIndex], _leftMats[rowIndex], width, outline, scale);
            if (rowIndex < _right.Length) ApplyFadeAndScale(_right[rowIndex], _rightMats[rowIndex], width, outline, scale);
        }
    }

    void ApplyFadeAndScale(TextMeshProUGUI label, Material mat, float width, Color outline, Vector3 scale)
    {
        if (!label || !mat) return;
        mat.SetFloat(ID_OutlineWidth, width);
        mat.SetColor(ID_OutlineColor, outline);
        label.rectTransform.localScale = scale;
    }

    void SetLabelOutline(TextMeshProUGUI label, Material mat, float width, Color outline, bool tintFace)
    {
        if (!label || !mat) return;
        mat.SetFloat(ID_OutlineWidth, width);
        mat.SetColor(ID_OutlineColor, outline);
        if (tintFace)
        {
            var face = label.color;
            var tint = faceHighlightTint;
            tint.a = face.a;
            mat.SetColor(ID_FaceColor, tint);
        }
    }

    void ResetLabel(TextMeshProUGUI label, Material mat)
    {
        if (!label || !mat) return;
        mat.SetFloat(ID_OutlineWidth, idleOutlineWidth);
        if (tintFaceOnHighlight)
        {
            var face = label.color;
            mat.SetColor(ID_FaceColor, face);
        }
        label.rectTransform.localScale = Vector3.one;
    }

    void ClearAll()
    {
        for (int i = 0; i < _top.Length; i++) ResetLabel(_top[i], _topMats[i]);
        for (int i = 0; i < _bottom.Length; i++) ResetLabel(_bottom[i], _bottomMats[i]);
        for (int i = 0; i < _left.Length; i++) ResetLabel(_left[i], _leftMats[i]);
        for (int i = 0; i < _right.Length; i++) ResetLabel(_right[i], _rightMats[i]);
        _lastCol = -1; _lastRow = -1;
    }

    public void Highlight(int colIndex, int rowIndex, Color outlineColor)
    {
        if (colIndex == _lastCol && rowIndex == _lastRow) return;
        if (_lastCol >= 0)
        {
            if (_lastCol < _top.Length) ResetLabel(_top[_lastCol], _topMats[_lastCol]);
            if (_lastCol < _bottom.Length) ResetLabel(_bottom[_lastCol], _bottomMats[_lastCol]);
        }
        if (_lastRow >= 0)
        {
            if (_lastRow < _left.Length) ResetLabel(_left[_lastRow], _leftMats[_lastRow]);
            if (_lastRow < _right.Length) ResetLabel(_right[_lastRow], _rightMats[_lastRow]);
        }
        if (colIndex >= 0)
        {
            if (colIndex < _top.Length) SetLabelOutline(_top[colIndex], _topMats[colIndex], highlightOutlineWidth, outlineColor, tintFaceOnHighlight);
            if (colIndex < _bottom.Length) SetLabelOutline(_bottom[colIndex], _bottomMats[colIndex], highlightOutlineWidth, outlineColor, tintFaceOnHighlight);
        }
        if (rowIndex >= 0)
        {
            if (rowIndex < _left.Length) SetLabelOutline(_left[rowIndex], _leftMats[rowIndex], highlightOutlineWidth, outlineColor, tintFaceOnHighlight);
            if (rowIndex < _right.Length) SetLabelOutline(_right[rowIndex], _rightMats[rowIndex], highlightOutlineWidth, outlineColor, tintFaceOnHighlight);
        }
        _lastCol = colIndex;
        _lastRow = rowIndex;
    }

    public void Clear()
    {
        if (_lastCol >= 0)
        {
            if (_lastCol < _top.Length) ResetLabel(_top[_lastCol], _topMats[_lastCol]);
            if (_lastCol < _bottom.Length) ResetLabel(_bottom[_lastCol], _bottomMats[_lastCol]);
        }
        if (_lastRow >= 0)
        {
            if (_lastRow < _left.Length) ResetLabel(_left[_lastRow], _leftMats[_lastRow]);
            if (_lastRow < _right.Length) ResetLabel(_right[_lastRow], _rightMats[_lastRow]);
        }
        _lastCol = -1;
        _lastRow = -1;
    }
}
