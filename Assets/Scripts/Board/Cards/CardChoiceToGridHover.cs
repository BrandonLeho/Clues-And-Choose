using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ChoiceGridCoord))]
public class CardChoiceToGridHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Optional")]
    public bool requireFrontFacing = false;
    public bool isFrontFacing = true;

    ChoiceGridCoord _coord;
    Image _img;

    void Awake()
    {
        _coord = GetComponent<ChoiceGridCoord>();
        _img = GetComponentInChildren<Image>(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (requireFrontFacing && !isFrontFacing) return;
        if (!_coord) return;
        var relay = GridHoverRelay.Instance;
        if (!relay) return;

        var c = _img ? _img.color : Color.white;
        relay.HoverEnter(_coord.col, _coord.row, c);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var relay = GridHoverRelay.Instance;
        if (!relay) return;
        relay.HoverExit();
    }
}


