using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
[DisallowMultipleComponent]
public class CoinPlacementProbe : MonoBehaviour
{
    public static CoinPlacementProbe Active { get; private set; }
    public static bool ProbeMode => Active != null;

    [Header("Probe")]
    public Vector2 probeOffsetLocal = new Vector2(0f, -0.6f);

    [Header("Arrow")]
    public Transform arrowPrefab;
    public Vector2 arrowOffsetLocal = new Vector2(0f, -0.6f);
    public float arrowLocalZ = 0f;
    public bool arrowUseProbeDirection = false;
    public float arrowRotationLocal = 0f;
    public bool alignSortingWithCoin = true;
    public int arrowSortingOrderDelta = -1;

    [Header("Arrow Lag Settings")]
    public float arrowLagSpeed = 10f;
    public float arrowRotationLagSpeed = 10f;

    [Header("Visibility")]
    public Camera uiCamera;
    public RectTransform gridMask;
    public bool requireInsideGridToShow = true;
    public bool startHiddenOnPickup = true;

    bool _suppressUntilInside;
    bool _arrowShown;
    CoinDragHandler _drag;
    Transform _arrowInst;
    SpriteRenderer _arrowSR;
    SpriteRenderer _coinSR;
    bool _isDragging;

    Vector3 _arrowSmoothedPos;
    Quaternion _arrowSmoothedRot;

    public Vector3 GetProbeWorld() =>
        transform.TransformPoint(new Vector3(probeOffsetLocal.x, probeOffsetLocal.y, 0f));

    public Vector2 GetProbeScreenPosition()
    {
        var cam = uiCamera ? uiCamera : Camera.main;
        return cam ? (Vector2)cam.WorldToScreenPoint(GetProbeWorld()) : (Vector2)GetProbeWorld();
    }

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        _coinSR = GetComponent<SpriteRenderer>();
        if (_drag)
        {
            _drag.onPickUp.AddListener(OnPickUp);
            _drag.onDrop.AddListener(OnDrop);
        }

        if (!gridMask)
        {
            var found = GameObject.Find("ColorGrid");
            if (found)
                gridMask = found.GetComponent<RectTransform>();
        }
    }

    void OnDestroy()
    {
        if (_drag)
        {
            _drag.onPickUp.RemoveListener(OnPickUp);
            _drag.onDrop.RemoveListener(OnDrop);
        }
    }

    void OnPickUp()
    {
        Active = this;
        _isDragging = true;
        if (arrowPrefab)
        {
            _arrowInst = Instantiate(arrowPrefab, transform);
            _arrowSR = _arrowInst.GetComponentInChildren<SpriteRenderer>();
            if (alignSortingWithCoin && _coinSR && _arrowSR)
            {
                _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
                _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
            }

            _arrowSmoothedPos = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);
            _arrowSmoothedRot = Quaternion.Euler(0f, 0f, arrowRotationLocal);

            _suppressUntilInside = startHiddenOnPickup;
            SetArrowShown(false);
        }
    }

    void OnDrop()
    {
        _isDragging = false;
        if (Active == this) Active = null;
        if (_arrowInst) Destroy(_arrowInst.gameObject);
        _arrowInst = null;
        _arrowSR = null;
        _arrowShown = false;
        _suppressUntilInside = false;
    }

    void Update()
    {
        if (!_isDragging || !_arrowInst) return;

        UpdateArrowLag();

        if (alignSortingWithCoin && _coinSR && _arrowSR)
        {
            _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
            _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
        }

        bool inside = IsProbeInsideGrid();

        if (_suppressUntilInside)
        {
            if (inside)
            {
                _suppressUntilInside = false;
                SetArrowShown(true);
            }
            else
            {
                SetArrowShown(false);
            }
        }
        else
        {
            SetArrowShown(inside);
        }
    }

    void UpdateArrowLag()
    {
        Vector3 targetPos = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);
        _arrowSmoothedPos = Vector3.Lerp(_arrowSmoothedPos, targetPos, Time.deltaTime * arrowLagSpeed);

        Quaternion targetRot;
        if (arrowUseProbeDirection)
        {
            Vector2 dir = probeOffsetLocal.sqrMagnitude > 1e-6f ? probeOffsetLocal : Vector2.down;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            targetRot = Quaternion.Euler(0f, 0f, ang);
        }
        else
        {
            targetRot = Quaternion.Euler(0f, 0f, arrowRotationLocal);
        }
        _arrowSmoothedRot = Quaternion.Lerp(_arrowSmoothedRot, targetRot, Time.deltaTime * arrowRotationLagSpeed);

        _arrowInst.localPosition = _arrowSmoothedPos;
        _arrowInst.localRotation = _arrowSmoothedRot;
    }

    bool IsProbeInsideGrid()
    {
        if (!requireInsideGridToShow) return true;
        if (!gridMask) return true;
        var cam = uiCamera ? uiCamera : Camera.main;
        Vector2 sp = GetProbeScreenPosition();
        return RectTransformUtility.RectangleContainsScreenPoint(gridMask, sp, cam);
    }

    void SetArrowShown(bool shown)
    {
        if (_arrowInst == null) { _arrowShown = false; return; }
        _arrowInst.gameObject.SetActive(shown);
        _arrowShown = shown;
    }
}
