using UnityEngine;
using TMPro;

public class RouletteTextItem : MonoBehaviour
{
    public TextMeshProUGUI tmp;
    RectTransform _rt;
    float _width;

    public RectTransform Rect => _rt ??= (RectTransform)transform;
    public float Width => _width;
    public string CurrentText { get; private set; }

    public void SetText(string text, float measuredWidth)
    {
        if (_rt == null) _rt = (RectTransform)transform;
        CurrentText = text;
        tmp.text = text;

        _width = measuredWidth;
        var size = _rt.sizeDelta;
        size.x = _width;
        _rt.sizeDelta = size;
    }
}
