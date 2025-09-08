using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class DraggableCoin : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Canvas / Raycasting")]
    public Canvas rootCanvas;
    public GraphicRaycaster raycaster;
    public Transform dragLayer;

    [Header("Behavior")]
    public float returnDuration = 0.15f;
    public bool reparentToCell = true;
    [SerializeField] float returnSpeed = 1200f;
    [SerializeField] AnimationCurve returnEase = AnimationCurve.EaseInOut(0, 0, 1, 1);


    [Header("Filters (optional)")]
    public LayerMask uiLayerMask = ~0;
    RectTransform _rt;
    CanvasGroup _cg;
    Camera _uiCam;
    Transform _startParent;
    Vector2 _startAnchored;
    DropCellUI _currentCell;
    CoinMakerUI _coinFX;

    bool _dragging;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _cg = GetComponent<CanvasGroup>();
        _coinFX = GetComponent<CoinMakerUI>();

        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        if (!raycaster && rootCanvas) raycaster = rootCanvas.GetComponent<GraphicRaycaster>();
        _uiCam = rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true;

        _startParent = _rt.parent;
        _startAnchored = _rt.anchoredPosition;

        _cg.blocksRaycasts = false;

        if (dragLayer) _rt.SetParent(dragLayer, worldPositionStays: true);

        _coinFX?.SetHover(true);
        if (_currentCell) { _currentCell.Release(this, this); }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!rootCanvas) return;
        var canvasRT = rootCanvas.transform as RectTransform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, eventData.position, _uiCam, out var local))
        {
            _rt.anchoredPosition = local;
        }

        HighlightCellUnderPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging = false;
        _cg.blocksRaycasts = true;
        ClearHighlights();

        var cell = RaycastForCell(eventData);
        if (cell != null && cell.IsAvailable())
        {
            if (reparentToCell)
            {
                _rt.SetParent(cell.GetSnapAnchor(), worldPositionStays: false);
                _rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                Vector2 targetAnchored = AnchoredPosInParentFrom(cell.GetSnapAnchor());
                _rt.anchoredPosition = targetAnchored;
            }

            cell.Reserve(this);
            _currentCell = cell;

            _coinFX?.FlashOnPlace();
            _coinFX?.SetHover(false);
            return;
        }

        StartCoroutine(EaseBack(_startParent as RectTransform, _startAnchored, 0f));

    }

    public void OnPointerEnter(PointerEventData eventData) => _coinFX?.SetHover(true);
    public void OnPointerExit(PointerEventData eventData) { if (!_dragging) _coinFX?.SetHover(false); }

    DropCellUI RaycastForCell(PointerEventData eventData)
    {
        if (!raycaster) return null;

        var results = new List<RaycastResult>(16);
        raycaster.Raycast(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (((1 << results[i].gameObject.layer) & uiLayerMask) == 0) continue;

            var cell = results[i].gameObject.GetComponentInParent<DropCellUI>();
            if (cell) return cell;
        }
        return null;
    }

    void HighlightCellUnderPointer(PointerEventData eventData)
    {
        var cell = RaycastForCell(eventData);
        if (cell == _currentCell) return;

        ClearHighlights();
        if (cell) cell.SetHover(true);
    }

    void ClearHighlights()
    {
        if (!raycaster) return;
        var roots = raycaster.gameObject.GetComponentsInChildren<DropCellUI>(true);
        foreach (var c in roots) c.SetHover(false);
        _currentCell?.SetHover(false);
    }

    IEnumerator EaseBack(RectTransform targetParent, Vector2 targetAnchored, float _ignored)
    {
        Vector2 startAnchoredInTarget = AnchoredInParent(targetParent);

        _rt.SetParent(targetParent, worldPositionStays: true);

        Vector2 from = startAnchoredInTarget;
        Vector2 to = targetAnchored;

        float dist = Vector2.Distance(from, to);
        float dur = Mathf.Max(0.01f, dist / Mathf.Max(1f, returnSpeed));

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float e = returnEase.Evaluate(p);
            _rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            yield return null;
        }

        _rt.anchoredPosition = to;
    }

    Vector2 AnchoredInParent(RectTransform targetParent)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(_uiCam, _rt.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetParent, screen, _uiCam, out var local);
        return local;
    }

    Vector2 AnchoredPosInParentFrom(RectTransform targetAnchor)
    {
        Vector3 world = targetAnchor.TransformPoint(Vector3.zero);
        RectTransform parentRT = _rt.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT,
            RectTransformUtility.WorldToScreenPoint(_uiCam, world),
            _uiCam,
            out var local);
        return local;
    }
}
