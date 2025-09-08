using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DropCellUI : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("If set, the coin will snap to this child (0,0) in anchored space. If null, it snaps to this RectTransform's center.")]
    public RectTransform snapAnchor;

    [Header("Rules")]
    [Tooltip("Only one coin may occupy this cell.")]
    public bool singleOccupancy = true;

    [Header("Optional Visuals")]
    public Graphic hoverHighlight;
    [Range(0f, 1f)] public float hoverAlpha = 0.6f;

    RectTransform _rt;
    Object _occupant;

    void Awake()
    {
        _rt = transform as RectTransform;
        if (!snapAnchor) snapAnchor = _rt;
        SetHover(false);
    }

    public bool IsAvailable() => !singleOccupancy || _occupant == null;

    public void Reserve(Object coin) { if (singleOccupancy) _occupant = coin; }
    public void Release(Object coin, DraggableCoin draggableCoin) { if (singleOccupancy && _occupant == coin) _occupant = null; }
    public bool IsOccupiedBy(Object o) => _occupant == o;

    public RectTransform GetSnapAnchor() => snapAnchor;

    public void SetHover(bool on)
    {
        if (!hoverHighlight) return;
        var c = hoverHighlight.color;
        c.a = on ? hoverAlpha : 0f;
        hoverHighlight.color = c;
    }
}
