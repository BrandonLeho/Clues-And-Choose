using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // only needed if you use LayoutRebuilder

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
        // Safety checks
        if (eventData == null || eventData.pointerDrag == null) return;

        var dropped = eventData.pointerDrag;
        var draggable = dropped.GetComponent<DraggableItem>();
        if (draggable == null) return; // not a draggable item

        if (acceptOnlyWhenEmpty && transform.childCount > 0)
            return;

        // Reparent under this slot (keeps it in the UI hierarchy; DraggableItem will clean up its temp canvas on EndDrag)
        dropped.transform.SetParent(transform, worldPositionStays: false);

        // Optional: keep as top-most child within this slot
        if (setAsLastSibling)
            dropped.transform.SetAsLastSibling();

        // Optional: snap to slot center and normalize transform
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

        // Optional: if you're using layout groups and want instant refresh
        // LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
