using UnityEngine;
using UnityEngine.EventSystems;

public class Slot : MonoBehaviour, IDropHandler
{
    [Header("Behavior")]
    [Tooltip("If true, ignores drops when this slot already has a child.")]
    public bool acceptOnlyWhenEmpty = true;

    [Tooltip("If true, re-center the dropped item in this slot.")]
    public bool snapToCenter = true;

    [Tooltip("If true, set dropped item as last sibling in this slot.")]
    public bool setAsLastSibling = true;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerDrag == null) return;

        var dropped = eventData.pointerDrag;
        var draggable = dropped.GetComponent<DraggableItem>();
        if (draggable == null) return;

        if (acceptOnlyWhenEmpty && transform.childCount > 0)
            return;


        dropped.transform.SetParent(transform, worldPositionStays: false);

        if (setAsLastSibling)
            dropped.transform.SetAsLastSibling();

        if (snapToCenter)
        {
            var rt = dropped.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }
        }
    }
}
