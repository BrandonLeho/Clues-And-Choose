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
    [Range(0.05f, 30f)] public float followSpeed = 20f;
    public float dragZ = 0f;
    public bool preserveGrabOffset = true;

    [Header("Visual Layering")]
    public SpriteRenderer targetRenderer;
    public int sortingOrderBoost = 50;

    [Header("Collisions")]
    public bool disableColliderWhileDragging = false;

    [Header("Scaling Effect")]
    [Tooltip("How much to scale coin while dragging. 1 = no change.")]
    [Range(0.8f, 1.5f)] public float pickupScaleMultiplier = 1.15f;
    [Tooltip("How quickly the coin scales to target size.")]
    [Range(1f, 20f)] public float scaleLerpSpeed = 10f;

    [Header("Events")]
    public UnityEvent onPickUp;
    public UnityEvent onDrop;

    Collider2D _col;
    int _activePointerId = -1;
    bool _isDragging;
    Vector3 _grabOffsetLocal;
    int _baseSortingOrder;
    bool _hadRenderer;
    float _origZ;

    Vector3 _baseScale;
    Vector3 _targetScale;

    NetworkCoin _netCoin;
    bool _allowLocalMove;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (!worldCamera) worldCamera = Camera.main;
        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        _hadRenderer = targetRenderer != null;
        if (_hadRenderer) _baseSortingOrder = targetRenderer.sortingOrder;
        _origZ = transform.position.z;
        _baseScale = transform.localScale;
        _targetScale = _baseScale;

        _netCoin = GetComponent<NetworkCoin>();
    }

    void Update()
    {
        if (Input.touchSupported && Input.touchCount > 0) HandleTouch();
        else HandleMouse();

        if (_isDragging && _allowLocalMove)
        {
            Vector3 targetPos = GetPointerWorld(_activePointerId);
            if (preserveGrabOffset) targetPos += _grabOffsetLocal;
            targetPos.z = dragZ;
            transform.position = Vector3.Lerp(transform.position, targetPos,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale,
            1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime));
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
                BeginDrag(mouseId, p2);
            if (_isDragging && (!mouseHeld || mouseUp))
                EndDrag();
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
                    BeginDrag(i, p2);
                    break;
                }
            }
        }

        if (_isDragging && _activePointerId >= 0 && _activePointerId < Input.touchCount)
        {
            var t = Input.GetTouch(_activePointerId);
            if (dragMode == DragMode.Hold &&
                (t.phase == TouchPhase.Canceled || t.phase == TouchPhase.Ended))
                EndDrag();

            if (dragMode == DragMode.Toggle && t.phase == TouchPhase.Began)
            {
                Vector2 p2 = (Vector2)GetPointerWorld(_activePointerId);
                if (_col && _col.OverlapPoint(p2))
                    EndDrag();
            }
        }
    }
    #endregion

    void BeginDrag(int pointerId, Vector2 worldPoint)
    {
        if (_netCoin != null && !_netCoin.IsLocalOwner())
            return;

        _activePointerId = pointerId;
        _isDragging = true;
        _allowLocalMove = true;

        Vector3 coinPos = transform.position;
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

        _targetScale = _baseScale * pickupScaleMultiplier;

        onPickUp?.Invoke();
    }

    void EndDrag()
    {
        _isDragging = false;
        _activePointerId = -1;
        _allowLocalMove = false;

        if (_hadRenderer) targetRenderer.sortingOrder = _baseSortingOrder;
        if (disableColliderWhileDragging) _col.enabled = true;

        var pos = transform.position;
        pos.z = _origZ;
        transform.position = pos;

        _targetScale = _baseScale;

        onDrop?.Invoke();
    }

    Vector3 GetPointerWorld(int pointerId)
    {
        if (!worldCamera) worldCamera = Camera.main;
        Vector3 sp;
        if (pointerId == -999) sp = Input.mousePosition;
        else sp = (pointerId >= 0 && pointerId < Input.touchCount)
            ? (Vector3)Input.GetTouch(pointerId).position
            : Input.mousePosition;

        Vector3 wp = worldCamera.ScreenToWorldPoint(sp);
        wp.z = dragZ;
        return wp;
    }
}
