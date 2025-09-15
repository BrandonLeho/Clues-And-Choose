using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class CoinDragHandler : MonoBehaviour
{
    public enum DragMode { Hold, Toggle }

    [Header("General")]
    public DragMode dragMode = DragMode.Hold;
    public Camera worldCamera;
    [Tooltip("How quickly the coin follows the pointer (1 = instant).")]
    [Range(0.05f, 30f)] public float followSpeed = 20f;
    [Tooltip("Keep the coin at this Z while dragging.")]
    public float dragZ = 0f;
    [Tooltip("If true, the coin keeps the initial grab offset instead of snapping center to the pointer.")]
    public bool preserveGrabOffset = true;

    [Header("Sorting / Visual Layering")]
    public SpriteRenderer targetRenderer;
    [Tooltip("Sorting order increase while dragging so the coin renders on top.")]
    public int sortingOrderBoost = 50;

    [Header("Collisions")]
    [Tooltip("Disable the collider while dragging to avoid accidental overlaps.")]
    public bool disableColliderWhileDragging = false;

    [Header("Events")]
    public UnityEvent onPickUp;
    public UnityEvent onDrop;

    // State
    Collider2D _col;
    int _activePointerId = -1;
    bool _isDragging;
    Vector3 _grabOffsetLocal;
    int _baseSortingOrder;
    bool _hadRenderer;
    float _origZ;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (!worldCamera) worldCamera = Camera.main;

        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        _hadRenderer = targetRenderer != null;
        if (_hadRenderer) _baseSortingOrder = targetRenderer.sortingOrder;

        _origZ = transform.position.z;
    }

    void Update()
    {
        if (Input.touchSupported && Input.touchCount > 0) HandleTouch();
        else HandleMouse();

        if (_isDragging)
        {
            Vector3 targetPos = GetPointerWorld(_activePointerId);
            if (preserveGrabOffset) targetPos += _grabOffsetLocal;
            targetPos.z = dragZ;

            transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }
    }

    #region Mouse
    void HandleMouse()
    {
        const int mouseId = -999;
        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseHeld = Input.GetMouseButton(0);
        bool mouseUp = Input.GetMouseButtonUp(0);

        Vector2 p2 = (Vector2)GetPointerWorld(mouseId);

        bool overMe = _col && _col.OverlapPoint(p2);

        if (dragMode == DragMode.Hold)
        {
            if (!_isDragging && mouseDown && overMe)
            {
                BeginDrag(mouseId, p2);
            }
            if (_isDragging && (!mouseHeld || mouseUp))
            {
                EndDrag();
            }
        }
        else
        {
            if (mouseDown && overMe)
            {
                if (!_isDragging) BeginDrag(mouseId, p2);
                else EndDrag();
            }
        }
    }
    #endregion

    #region Touch
    void HandleTouch()
    {
        if (!_isDragging)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;

                Vector2 p2 = (Vector2)GetPointerWorld(i);
                if (_col && _col.OverlapPoint(p2))
                {
                    if (dragMode == DragMode.Hold || dragMode == DragMode.Toggle)
                    {
                        BeginDrag(i, p2);
                    }
                    break;
                }
            }
        }

        if (_isDragging)
        {
            if (_activePointerId >= 0 && _activePointerId < Input.touchCount)
            {
                var t = Input.GetTouch(_activePointerId);

                if (dragMode == DragMode.Hold)
                {
                    if (t.phase == TouchPhase.Canceled || t.phase == TouchPhase.Ended)
                    {
                        EndDrag();
                    }
                }
                else // Toggle
                {
                    // In toggle, lift doesn't drop; a second tap over the coin will.
                    // Detect second tap:
                    if (t.phase == TouchPhase.Ended)
                    {
                        // Do nothing here; wait for a new Began over us to toggle off
                        // (Handled in the “not dragging” block above: Began over us -> BeginDrag; if dragging, EndDrag)
                    }
                }
            }
            else
            {

                if (dragMode == DragMode.Hold) EndDrag();
            }

            if (dragMode == DragMode.Toggle)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began) continue;

                    Vector2 p2 = (Vector2)GetPointerWorld(i);
                    if (_col && _col.OverlapPoint(p2))
                    {
                        EndDrag();
                        break;
                    }
                }
            }
        }
    }
    #endregion

    #region Core
    void BeginDrag(int pointerId, Vector2 worldPoint)
    {
        _activePointerId = pointerId;
        _isDragging = true;

        var coinPos = transform.position;
        Vector3 pointer3 = new Vector3(worldPoint.x, worldPoint.y, dragZ);
        _grabOffsetLocal = preserveGrabOffset ? (coinPos - pointer3) : Vector3.zero;

        if (_hadRenderer)
        {
            _baseSortingOrder = targetRenderer.sortingOrder;
            targetRenderer.sortingOrder = _baseSortingOrder + sortingOrderBoost;
        }

        if (disableColliderWhileDragging) _col.enabled = false;

        _origZ = coinPos.z;
        var pos = transform.position;
        pos.z = dragZ;
        transform.position = pos;

        onPickUp?.Invoke();
    }

    void EndDrag()
    {
        _isDragging = false;
        _activePointerId = -1;

        if (_hadRenderer) targetRenderer.sortingOrder = _baseSortingOrder;
        if (disableColliderWhileDragging) _col.enabled = true;

        var pos = transform.position;
        pos.z = _origZ;
        transform.position = pos;

        onDrop?.Invoke();
    }

    Vector3 GetPointerWorld(int pointerId)
    {
        if (!worldCamera) worldCamera = Camera.main;
        Vector3 sp;
        if (pointerId == -999)
        {
            sp = Input.mousePosition;
        }
        else
        {
            sp = (pointerId >= 0 && pointerId < Input.touchCount)
                ? (Vector3)Input.GetTouch(pointerId).position
                : Input.mousePosition;
        }

        var wp = worldCamera.ScreenToWorldPoint(sp);
        wp.z = dragZ;
        return wp;
    }
    #endregion
}
