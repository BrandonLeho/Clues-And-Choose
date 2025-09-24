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
    [Range(0.8f, 1.5f)] public float pickupScaleMultiplier = 1.15f;
    [Range(1f, 20f)] public float scaleLerpSpeed = 10f;

    [Header("Debug")]
    public bool debugInteract = false;

    [Header("Events")]
    public UnityEvent onPickUp;
    public UnityEvent onDrop;

    [Header("Input Guard")]
    [Tooltip("Minimum time after pickup before a drop is allowed (prevents same-frame drop).")]
    [Range(0f, 0.2f)] public float dropGuardSeconds = 0.06f;
    float _noDropBefore;

    Collider2D _col;
    int _activePointerId = -1;
    bool _isDragging;
    bool _allowLocalMove;
    Vector3 _grabOffsetLocal;
    int _baseSortingOrder;
    bool _hadRenderer;
    float _origZ;
    Vector3 _baseScale, _targetScale;

    NetworkCoin _netCoin;
    CoinDragSync _sync;

    ICoinDragPermission[] _permGuards;

    CoinRejectionFeedback _rejectFx;

    void Awake()
    {
        _rejectFx = GetComponent<CoinRejectionFeedback>();
        _permGuards = GetComponents<ICoinDragPermission>();
        _col = GetComponent<Collider2D>();
        _netCoin = GetComponent<NetworkCoin>();
        _sync = GetComponent<CoinDragSync>();

        if (!worldCamera)
        {
            worldCamera = Camera.main;
            if (debugInteract && !worldCamera) Debug.LogWarning($"[{name}] No worldCamera; set one at runtime.");
        }

        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        _hadRenderer = targetRenderer != null;
        if (_hadRenderer) _baseSortingOrder = targetRenderer.sortingOrder;

        if (Mathf.Approximately(dragZ, 0f)) dragZ = transform.position.z;

        _origZ = transform.position.z;
        _baseScale = transform.localScale;
        _targetScale = _baseScale;
    }

    void Update()
    {
        if (Input.touchSupported && Input.touchCount > 0) HandleTouch();
        else HandleMouse();

        if (_isDragging && _allowLocalMove)
        {
            Vector3 targetPos = PointerOnDragPlane(_activePointerId);
            if (preserveGrabOffset) targetPos += _grabOffsetLocal;
            targetPos.z = dragZ;

            transform.position = Vector3.Lerp(
                transform.position, targetPos,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        transform.localScale = Vector3.Lerp(
            transform.localScale, _targetScale,
            1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime));
    }

    #region Mouse
    void HandleMouse()
    {
        const int mouseId = -999;
        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseHeld = Input.GetMouseButton(0);
        bool mouseUp = Input.GetMouseButtonUp(0);

        Vector2 p2 = (Vector2)PointerOnDragPlane(mouseId);
        bool overMe = _col && _col.OverlapPoint(p2);

        if (dragMode == DragMode.Hold)
        {
            if (!_isDragging && mouseDown && overMe)
            {
                BeginDrag(mouseId, p2);
                return;
            }

            if (_isDragging)
            {
                bool canDrop = Time.unscaledTime >= _noDropBefore;
                if (canDrop && (!mouseHeld || mouseUp))
                {
                    EndDrag();
                    return;
                }
            }
        }
        else
        {
            if (mouseDown && overMe)
            {
                if (!_isDragging) { BeginDrag(mouseId, p2); return; }
                else { EndDrag(); return; }
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
                Vector2 p2 = (Vector2)PointerOnDragPlane(i);
                bool overMe = _col && _col.OverlapPoint(p2);
                if (debugInteract) Debug.Log($"[{name}] touch began overMe={overMe} id={i}");
                if (overMe) { BeginDrag(i, p2); break; }
            }
        }
        else
        {
            if (dragMode == DragMode.Hold)
            {
                if (_activePointerId >= 0 && _activePointerId < Input.touchCount)
                {
                    var t = Input.GetTouch(_activePointerId);
                    if (t.phase == TouchPhase.Canceled || t.phase == TouchPhase.Ended) EndDrag();
                }
                else EndDrag();
            }
            else
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began) continue;
                    Vector2 p2 = (Vector2)PointerOnDragPlane(i);
                    if (_col && _col.OverlapPoint(p2)) { EndDrag(); break; }
                }
            }
        }
    }
    #endregion

    void BeginDrag(int pointerId, Vector2 worldPoint)
    {
        if (debugInteract) Debug.Log($"[Drag] {name} BeginDrag called");

        if (_netCoin != null && !_netCoin.IsLocalOwner())
        {
            if (debugInteract) Debug.Log($"[Drag] {name} blocked: not local owner (owner={_netCoin.ownerNetId})");
            if (_rejectFx) _rejectFx.Play();
            return;
        }

        if (!GuardsAllowBeginDrag())
        {
            if (_rejectFx) _rejectFx.Play();
            return;
        }

        _activePointerId = pointerId;
        _isDragging = true;
        _allowLocalMove = true;

        _noDropBefore = Time.unscaledTime + dropGuardSeconds;

        if (debugInteract) Debug.Log($"[Drag] {name} STARTED: dragging=true allowLocalMove={_allowLocalMove}");

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
        var pos = transform.position; pos.z = dragZ; transform.position = pos;

        _targetScale = _baseScale * pickupScaleMultiplier;

        _sync?.BeginLocalDrag();
        onPickUp?.Invoke();
    }

    void EndDrag()
    {
        if (debugInteract) Debug.Log($"[Drag] {name} EndDrag()");

        _isDragging = false;
        _activePointerId = -1;
        _allowLocalMove = false;

        if (_hadRenderer) targetRenderer.sortingOrder = _baseSortingOrder;
        if (disableColliderWhileDragging) _col.enabled = true;

        var pos = transform.position; pos.z = _origZ; transform.position = pos;

        _targetScale = _baseScale;

        _sync?.EndLocalDrag();
        onDrop?.Invoke();
    }

    Vector3 PointerOnDragPlane(int pointerId)
    {
        if (!worldCamera) worldCamera = Camera.main;
        Vector3 sp = pointerId == -999
            ? (Vector3)Input.mousePosition
            : (pointerId >= 0 && pointerId < Input.touchCount)
                ? (Vector3)Input.GetTouch(pointerId).position
                : Input.mousePosition;

        Ray ray = worldCamera ? worldCamera.ScreenPointToRay(sp)
                              : new Ray(new Vector3(sp.x, sp.y, -10f), Vector3.forward);

        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, dragZ));
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            return new Vector3(hit.x, hit.y, dragZ);
        }

        var wp = worldCamera
            ? worldCamera.ScreenToWorldPoint(new Vector3(sp.x, sp.y,
                  worldCamera.orthographic ? worldCamera.nearClipPlane
                                           : Mathf.Abs(worldCamera.transform.position.z - dragZ)))
            : new Vector3(sp.x, sp.y, dragZ);
        wp.z = dragZ;
        return wp;
    }

    bool GuardsAllowBeginDrag()
    {
        if (_permGuards == null || _permGuards.Length == 0) return true;
        for (int i = 0; i < _permGuards.Length; i++)
            if (_permGuards[i] != null && !_permGuards[i].CanBeginDrag())
                return false;
        return true;
    }
}
