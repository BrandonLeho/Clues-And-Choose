using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Refs (optional)")]
    public Image image;              // If null, will auto-find on this GO
    public RectTransform rt;         // If null, will auto-get on this GO

    // Internals
    private Canvas parentCanvas;     // The canvas we live under
    private Canvas tempCanvas;       // Temporary canvas to force on-top while dragging
    private CanvasGroup cg;          // For toggling raycast blocking
    private Transform originalParent;
    private int originalSiblingIndex;

    void Awake()
    {
        if (!rt) rt = GetComponent<RectTransform>();
        if (!image) image = GetComponent<Image>();
        if (!cg) cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        parentCanvas = GetComponentInParent<Canvas>();
        if (!parentCanvas)
            Debug.LogWarning("[DraggableItem] No parent Canvas found. UI dragging may not render correctly.");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        // Let drops go through while dragging
        cg.blocksRaycasts = false;
        if (image) image.raycastTarget = false;

        // Give this element its own high-sorting canvas (keeps it in hierarchy but on top)
        tempCanvas = gameObject.AddComponent<Canvas>();
        tempCanvas.overrideSorting = true;
        tempCanvas.sortingOrder = 10000; // very high so it stays above most UI
        // Optional (not strictly needed): add a raycaster if you want it to receive pointer events itself
        if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();

        // Keep it under the same parent (so it never “disappears” from your hierarchy)
        // but push it to top of its siblings for good measure.
        transform.SetSiblingIndex(transform.parent.childCount - 1);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!parentCanvas) return;

        // Convert screen point -> local point in our canvas space so it tracks correctly
        RectTransform canvasRT = parentCanvas.transform as RectTransform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT,
            eventData.position,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out localPoint
        );

        // Position the item; since we're not reparenting, set position in world via anchoredPosition in canvas space
        // Move in the canvas' local space by converting to our own parent space
        // Easiest: set our position using parent canvas as reference
        Vector3 worldPos = parentCanvas.transform.TransformPoint(localPoint);
        rt.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Restore raycasts
        cg.blocksRaycasts = true;
        if (image) image.raycastTarget = true;

        // If the item was dropped into a new parent by some drop handler, great.
        // Otherwise, keep original hierarchy ordering.
        if (transform.parent == originalParent)
            transform.SetSiblingIndex(originalSiblingIndex);

        // Reset anchors to middle center
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; // centers it at parent

        // Remove the temporary sorting canvas so normal layering returns
        if (tempCanvas)
        {
            Destroy(tempCanvas.GetComponent<GraphicRaycaster>());
            Destroy(tempCanvas);
            tempCanvas = null;
        }
    }

}
