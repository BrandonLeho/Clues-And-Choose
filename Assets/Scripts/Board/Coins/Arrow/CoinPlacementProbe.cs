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
    public float tipRotationSmoothTime = 0.08f;
    public float tipTrailAngleBoost = 8f;
    public float velocityToDegrees = 2.0f;
    public Vector2 tipGraphicPivotNudgeLocal = Vector2.zero;
    public Camera uiCamera;
    public RectTransform gridMask;
    public bool requireInsideGridToShow = true;
    public bool startHiddenOnPickup = true;
    public float entryDuration = 0.15f;
    public float exitDuration = 0.15f;
    public float hiddenXAngle = 95f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    bool _suppressUntilInside;
    CoinDragHandler _drag;
    Transform _arrowInst;
    SpriteRenderer _arrowSR;
    Transform _tipGraphic;
    SpriteRenderer _coinSR;
    bool _isDragging;
    float _tipAngleVel;
    float _tipAngleCurrent;
    Vector3 _prevBaseWorld;
    bool _targetShown;
    float _animT;
    bool _animating;

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
            _arrowSR = _arrowInst.GetComponentInChildren<SpriteRenderer>();
            _tipGraphic = _arrowSR ? _arrowSR.transform : _arrowInst;

            if (alignSortingWithCoin && _coinSR && _arrowSR)
            {
                _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
                _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
            }

            _arrowInst.localPosition = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);

            float startAngleZ = arrowUseProbeDirection
                ? Mathf.Atan2(probeOffsetLocal.y, probeOffsetLocal.x) * Mathf.Rad2Deg
                : arrowRotationLocal;

            if (_tipGraphic != null)
            {
                _tipGraphic.localPosition = new Vector3(tipGraphicPivotNudgeLocal.x, tipGraphicPivotNudgeLocal.y, 0f);
                _tipGraphic.localRotation = Quaternion.Euler(0f, 0f, startAngleZ);
            }
            _tipAngleCurrent = startAngleZ;
            _tipAngleVel = 0f;
            _prevBaseWorld = _arrowInst.position;

            _suppressUntilInside = startHiddenOnPickup;
            _animT = 0f;
            _targetShown = false;
            _animating = false;
            _arrowInst.gameObject.SetActive(true);
            ApplyArrowPose();
        }
    }

    void OnDrop()
    {
        _isDragging = false;
        if (Active == this) Active = null;

        if (_arrowInst) Destroy(_arrowInst.gameObject);
        _arrowInst = null;
        _arrowSR = null;
        _tipGraphic = null;

        _suppressUntilInside = false;
        _animT = 0f;
        _animating = false;
        _targetShown = false;
    }

    void Update()
    {
        if (!_isDragging || !_arrowInst) return;

        _arrowInst.localPosition = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);

        if (alignSortingWithCoin && _coinSR && _arrowSR)
        {
            _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
            _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
        }

        UpdateTipLagRotation();

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

        TickArrowAnimator();
    }

    void UpdateTipLagRotation()
    {
        if (_tipGraphic == null) return;

        float targetAngleDeg = arrowUseProbeDirection
            ? Mathf.Atan2(probeOffsetLocal.y, probeOffsetLocal.x) * Mathf.Rad2Deg
            : arrowRotationLocal;

        Vector3 baseWorld = _arrowInst.position;
        Vector3 v3 = (baseWorld - _prevBaseWorld) / Mathf.Max(Time.deltaTime, 1e-5f);
        _prevBaseWorld = baseWorld;

        Vector2 aim = new Vector2(Mathf.Cos(targetAngleDeg * Mathf.Deg2Rad),
                                  Mathf.Sin(targetAngleDeg * Mathf.Deg2Rad));
        Vector2 v = new Vector2(v3.x, v3.y);

        float crossZ = aim.x * v.y - aim.y * v.x;
        float speed = v.magnitude;
        float signedExtra = -Mathf.Sign(crossZ) * speed * velocityToDegrees;
        float extra = Mathf.Clamp(signedExtra, -tipTrailAngleBoost, tipTrailAngleBoost);
        float targetWithTrail = targetAngleDeg + extra;

        _tipAngleCurrent = Mathf.SmoothDampAngle(
            _tipAngleCurrent,
            targetWithTrail,
            ref _tipAngleVel,
            Mathf.Max(0.0001f, tipRotationSmoothTime)
        );

        _tipGraphic.localRotation = Quaternion.Euler(0f, 0f, _tipAngleCurrent);
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
        if (_arrowInst == null) { _targetShown = false; return; }
        if (shown && !_arrowInst.gameObject.activeSelf)
            _arrowInst.gameObject.SetActive(true);

        _targetShown = shown;
        _animating = true;
    }

    void TickArrowAnimator()
    {
        if (_arrowInst == null) return;

        float target = _targetShown ? 1f : 0f;
        if (!Mathf.Approximately(_animT, target))
        {
            float dur = _targetShown ? Mathf.Max(0.0001f, entryDuration) : Mathf.Max(0.0001f, exitDuration);
            float step = Time.deltaTime / dur;
            _animT = Mathf.MoveTowards(_animT, target, step);
            _animating = true;
            ApplyArrowPose();
        }

        if (Mathf.Approximately(_animT, 0f) && !_targetShown)
        {
            ApplyArrowPose();
            if (_arrowInst.gameObject.activeSelf)
                _arrowInst.gameObject.SetActive(false);
            _animating = false;
        }

        if (Mathf.Approximately(_animT, 1f) && _targetShown)
        {
            ApplyArrowPose();
            _animating = false;
        }
    }

    void ApplyArrowPose()
    {
        if (_arrowInst == null) return;

        float t = Mathf.Clamp01(_animT);
        float e = ease != null ? ease.Evaluate(t) : t;
        float x = Mathf.LerpUnclamped(hiddenXAngle, 0f, e);
        _arrowInst.localRotation = Quaternion.Euler(x, 0f, 0f);
    }
}
