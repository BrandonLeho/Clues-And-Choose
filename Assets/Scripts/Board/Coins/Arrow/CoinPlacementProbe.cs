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

    [SerializeField] bool useTipLag = true;
    [SerializeField] string tipTransformName = "Tip";
    [SerializeField] float tipLagSeconds = 0.08f;
    [SerializeField] float tipDistanceLocalY = 1.0f;

    bool _suppressUntilInside;
    bool _arrowShown;
    CoinDragHandler _drag;
    Transform _arrowInst;
    SpriteRenderer _arrowSR;
    SpriteRenderer _coinSR;
    bool _isDragging;
    Transform _arrowTip;
    float _tipAngleSmoothed;
    float _tipAngleVel;

    public Vector3 GetProbeWorld() => transform.TransformPoint(new Vector3(probeOffsetLocal.x, probeOffsetLocal.y, 0f));
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
            if (found) gridMask = found.GetComponent<RectTransform>();
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
            _arrowTip = _arrowInst ? _arrowInst.Find(tipTransformName) : null;
            _tipAngleVel = 0f;
            if (_arrowInst) _tipAngleSmoothed = _arrowInst.localEulerAngles.z;
            if (_arrowTip)
            {
                _arrowTip.localPosition = new Vector3(0f, tipDistanceLocalY, 0f);
                _arrowTip.localEulerAngles = Vector3.zero;
            }

            _arrowSR = _arrowInst.GetComponentInChildren<SpriteRenderer>();
            if (alignSortingWithCoin && _coinSR && _arrowSR)
            {
                _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
                _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
            }

            ApplyArrowLocalTransformImmediate();
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
        _arrowTip = null;
        _tipAngleVel = 0f;
    }

    void Update()
    {
        if (!_isDragging || !_arrowInst) return;

        ApplyArrowLocalTransformImmediate();

        if (alignSortingWithCoin && _coinSR && _arrowSR)
        {
            _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
            _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
        }

        if (useTipLag && _arrowInst && _arrowTip)
        {
            float shaftZ = _arrowInst.localEulerAngles.z;
            _tipAngleSmoothed = Mathf.SmoothDampAngle(_tipAngleSmoothed, shaftZ, ref _tipAngleVel, Mathf.Max(0.0001f, tipLagSeconds));
            float tipLocalZ = Mathf.DeltaAngle(0f, _tipAngleSmoothed - shaftZ);
            _arrowTip.localEulerAngles = new Vector3(0f, 0f, tipLocalZ);
            _arrowTip.localPosition = new Vector3(0f, tipDistanceLocalY, 0f);
        }

        bool inside = IsProbeInsideGrid();
        if (_suppressUntilInside)
        {
            if (inside)
            {
                _suppressUntilInside = false;
                SetArrowShown(true);
            }
            else SetArrowShown(false);
        }
        else SetArrowShown(inside);
    }

    void ApplyArrowLocalTransformImmediate()
    {
        if (!_arrowInst) return;
        _arrowInst.localPosition = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);

        if (arrowUseProbeDirection)
        {
            Vector2 dir = probeOffsetLocal.sqrMagnitude > 1e-6f ? probeOffsetLocal : Vector2.down;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _arrowInst.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
        else _arrowInst.localRotation = Quaternion.Euler(0f, 0f, arrowRotationLocal);
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
        if (!_arrowInst) { _arrowShown = false; return; }
        _arrowInst.gameObject.SetActive(shown);
        _arrowShown = shown;
    }
}
