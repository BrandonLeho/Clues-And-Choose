using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
[DisallowMultipleComponent]
public class CoinPlacementProbe : MonoBehaviour
{
    public static CoinPlacementProbe Active { get; private set; }
    public static bool ProbeMode => Active != null;

    public Vector2 probeOffsetLocal = new Vector2(0f, -0.6f);

    public Transform arrowPrefab;
    public Vector2 arrowOffsetLocal = new Vector2(0f, -0.6f);
    public float arrowLocalZ = 0f;

    public bool arrowUseProbeDirection = false;
    public float arrowRotationLocal = 0f;

    public bool alignSortingWithCoin = true;
    public int arrowSortingOrderDelta = -1;

    public Camera uiCamera;
    public RectTransform gridMask;
    public bool requireInsideGridToShow = true;
    public bool startHiddenOnPickup = true;

    [Header("Lag")]
    public float positionSmoothTime = 0.08f;
    public float rotationSmoothTime = 0.08f;
    public bool useVelocityForAim = true;
    public float trailAngleBoost = 8f;

    bool _suppressUntilInside;
    bool _arrowShown;
    CoinDragHandler _drag;
    SpriteRenderer _coinSR;

    Transform _arrowInst;
    SpriteRenderer _arrowSR;

    Vector3 _vel;
    float _angVel;
    bool _isDragging;

    public Vector3 GetProbeWorld()
        => transform.TransformPoint(new Vector3(probeOffsetLocal.x, probeOffsetLocal.y, 0f));

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

            _arrowInst.localPosition = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);
            _arrowInst.localRotation = Quaternion.Euler(0f, 0f, arrowRotationLocal);

            _arrowInst.SetParent(null, true);

            if (alignSortingWithCoin && _coinSR && _arrowSR)
            {
                _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
                _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
            }

            _vel = Vector3.zero;
            _angVel = 0f;

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

        if (alignSortingWithCoin && _coinSR && _arrowSR)
        {
            _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
            _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
        }

        Vector3 targetWorldPos = transform.TransformPoint(new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ));

        Vector3 newPos = Vector3.SmoothDamp(_arrowInst.position, targetWorldPos, ref _vel, Mathf.Max(0.0001f, positionSmoothTime));
        _arrowInst.position = newPos;

        float targetAngleDeg;
        if (arrowUseProbeDirection)
        {
            if (useVelocityForAim && _vel.sqrMagnitude > 0.000001f)
            {
                Vector2 v = new Vector2(_vel.x, _vel.y);
                targetAngleDeg = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                targetAngleDeg += Mathf.Clamp(v.magnitude * trailAngleBoost, -trailAngleBoost, trailAngleBoost);
            }
            else
            {
                Vector2 dir = probeOffsetLocal.sqrMagnitude > 1e-6f ? probeOffsetLocal : Vector2.down;
                targetAngleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
        }
        else
        {
            targetAngleDeg = arrowRotationLocal;
        }

        float currentZ = _arrowInst.eulerAngles.z;
        float newZ = Mathf.SmoothDampAngle(currentZ, targetAngleDeg, ref _angVel, Mathf.Max(0.0001f, rotationSmoothTime));
        _arrowInst.rotation = Quaternion.Euler(0f, 0f, newZ);

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
