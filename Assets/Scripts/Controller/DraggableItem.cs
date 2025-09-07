using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Refs")]
    public Image image;
    public RectTransform rt;

    // Internals
    private Canvas parentCanvas;
    private Canvas tempCanvas;
    private CanvasGroup cg;
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

        cg.blocksRaycasts = false;
        if (image) image.raycastTarget = false;

        tempCanvas = gameObject.AddComponent<Canvas>();
        tempCanvas.overrideSorting = true;
        tempCanvas.sortingOrder = 10000;

        if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();

        transform.SetSiblingIndex(transform.parent.childCount - 1);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!parentCanvas) return;

        RectTransform canvasRT = parentCanvas.transform as RectTransform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT,
            eventData.position,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out localPoint
        );

        Vector3 worldPos = parentCanvas.transform.TransformPoint(localPoint);
        rt.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        cg.blocksRaycasts = true;
        if (image) image.raycastTarget = true;

        if (transform.parent == originalParent)
            transform.SetSiblingIndex(originalSiblingIndex);

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        if (tempCanvas)
        {
            Destroy(tempCanvas.GetComponent<GraphicRaycaster>());
            Destroy(tempCanvas);
            tempCanvas = null;
        }
    }

}
