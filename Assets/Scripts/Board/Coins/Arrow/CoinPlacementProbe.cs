using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
[DisallowMultipleComponent]
public class CoinPlacementProbe : MonoBehaviour
{
    public static CoinPlacementProbe Active { get; private set; }
    public static bool ProbeMode => Active != null;

    [Header("Probe Settings")]
    public Vector2 probeOffsetLocal = new Vector2(0f, -0.6f);

    [Header("Arrow References & Positioning")]
    public Transform arrowPrefab;
    public Vector2 arrowOffsetLocal = new Vector2(0f, -0.6f);
    public float arrowLocalZ = 0f;
    public bool arrowUseProbeDirection = false;
    public float arrowRotationLocal = 0f;

    [Header("Arrow Rendering")]
    public bool alignSortingWithCoin = true;
    public bool forceArrowBelow = true;
    [Min(1)] public int arrowBelowOffset = 1;

    [Header("Arrow Tip Animation")]
    public float tipRotationSmoothTime = 0.08f;
    public float tipTrailAngleBoost = 8f;
    public float velocityToDegrees = 2.0f;
    public Vector2 tipGraphicPivotNudgeLocal = Vector2.zero;

    [Header("UI & Grid Settings")]
    public Camera uiCamera;
    public RectTransform gridMask;
    public bool requireInsideGridToShow = true;
    public bool startHiddenOnPickup = true;

    [Header("Arrow Show/Hide Animation")]
    public float entryDuration = 0.15f;
    public float exitDuration = 0.15f;
    public float hiddenXAngle = 95f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Isolation Mask")]
    public bool useIsolationMask = true;
    public Vector2 maskStretch = new Vector2(0.18f, 0.12f);
    [Range(0.0001f, 0.5f)]
    public float maskFeather = 0.15f;
    [Range(0f, 1f)]
    public float maskOpacity = 1f;
    public Color maskColor = Color.black;
    public bool maskFollowsArrowVisibility = true;

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

    CoinDragSync _netDrag;
    bool _remoteMode;

    GameObject _maskGO;
    SpriteRenderer _maskSR;
    Material _maskMat;
    Texture2D _maskTex;
    Camera _worldCam;
    int _propCenter, _propRadius, _propFeather, _propColor;

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
        _netDrag = GetComponent<CoinDragSync>();
        _worldCam = Camera.main;

        if (_drag)
        {
            _drag.onPickUp.AddListener(OnPickUp);
            _drag.onDrop.AddListener(OnDrop);
        }
        if (_netDrag)
        {
            _netDrag.DragStateChanged += OnNetDragStateChanged;
        }

        if (!gridMask)
        {
            var found = GameObject.Find("ColorGrid");
            if (found) gridMask = found.GetComponent<RectTransform>();
        }

        _propCenter = Shader.PropertyToID("_Center");
        _propRadius = Shader.PropertyToID("_Radius");
        _propFeather = Shader.PropertyToID("_Feather");
        _propColor = Shader.PropertyToID("_OverlayColor");
    }

    void OnDestroy()
    {
        if (_drag)
        {
            _drag.onPickUp.RemoveListener(OnPickUp);
            _drag.onDrop.RemoveListener(OnDrop);
        }
        if (_netDrag)
        {
            _netDrag.DragStateChanged -= OnNetDragStateChanged;
        }
        DestroyIsolationMask();
    }

    void OnPickUp()
    {
        _remoteMode = false;
        StartArrow(showAsLocal: true);
    }

    void OnDrop()
    {
        StopArrow(isLocalCall: true);
    }

    void OnNetDragStateChanged(bool dragging)
    {
        if (_netDrag != null && _netDrag.IsLocalOwner) return;

        if (dragging)
        {
            _remoteMode = true;
            StartArrow(showAsLocal: false);
        }
        else
        {
            StopArrow(isLocalCall: false);
        }
    }

    void StartArrow(bool showAsLocal)
    {
        _isDragging = true;

        if (showAsLocal)
        {
            Active = this;
        }

        if (arrowPrefab && !_arrowInst)
        {
            _arrowInst = Instantiate(arrowPrefab, transform);
            _arrowSR = _arrowInst.GetComponentInChildren<SpriteRenderer>();
            _tipGraphic = _arrowSR ? _arrowSR.transform : _arrowInst;

            SyncArrowSortingLayerAndOrder();

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

            _suppressUntilInside = showAsLocal ? startHiddenOnPickup : false;
            _animT = 0f;
            _targetShown = false;
            _animating = false;
            _arrowInst.gameObject.SetActive(true);
            ApplyArrowPose();
        }

        if (useIsolationMask && showAsLocal)
        {
            EnsureIsolationMask();
            UpdateIsolationMaskVisibility(forceHidden: maskFollowsArrowVisibility && _suppressUntilInside);
        }
    }

    void StopArrow(bool isLocalCall)
    {
        _isDragging = false;

        if (isLocalCall)
        {
            if (Active == this) Active = null;
        }

        if (_arrowInst) Destroy(_arrowInst.gameObject);
        _arrowInst = null;
        _arrowSR = null;
        _tipGraphic = null;

        _suppressUntilInside = false;
        _animT = 0f;
        _animating = false;
        _targetShown = false;
        _remoteMode = false;

        if (isLocalCall) DestroyIsolationMask();
    }

    void Update()
    {
        if (!_isDragging) return;

        if (_arrowInst)
        {
            _arrowInst.localPosition = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);
            SyncArrowSortingLayerAndOrder();
            UpdateTipLagRotation();

            bool inside = IsProbeInsideGrid();
            if (_suppressUntilInside)
            {
                if (inside)
                {
                    _suppressUntilInside = false;
                    SetArrowShown(true);
                    UpdateIsolationMaskVisibility(forceHidden: false);
                }
                else
                {
                    SetArrowShown(false);
                    UpdateIsolationMaskVisibility(forceHidden: true);
                }
            }
            else
            {
                SetArrowShown(inside);
                if (maskFollowsArrowVisibility)
                    UpdateIsolationMaskVisibility(forceHidden: !inside);
            }

            TickArrowAnimator();
        }

        if (_maskMat != null)
        {
            UpdateIsolationMask();
        }
    }

    void SyncArrowSortingLayerAndOrder()
    {
        if (!alignSortingWithCoin || _coinSR == null || _arrowSR == null) return;

        _arrowSR.sortingLayerID = _coinSR.sortingLayerID;

        if (forceArrowBelow)
        {
            int coinOrder = _coinSR.sortingOrder;
            _arrowSR.sortingOrder = coinOrder - Mathf.Max(1, arrowBelowOffset);
        }
        else
        {
            _arrowSR.sortingOrder = _coinSR.sortingOrder;
        }
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

    void EnsureIsolationMask()
    {
        if (_maskGO != null) return;

        var shader = Shader.Find("Hidden/ScreenHoleFeather");
        if (shader == null)
        {
            Debug.LogError("Missing shader Hidden/ScreenHoleFeather. Please add the shader file included below to your project.");
            return;
        }
        _maskMat = new Material(shader);

        _maskTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        _maskTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        _maskTex.Apply();

        var sprite = Sprite.Create(_maskTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);

        _maskGO = new GameObject("ArrowIsolationMask");
        _maskGO.transform.SetParent(transform, worldPositionStays: false);

        _maskSR = _maskGO.AddComponent<SpriteRenderer>();
        _maskSR.sprite = sprite;

        int coinOrder = _coinSR ? _coinSR.sortingOrder : 50000;
        int arrowOrder = (_arrowSR != null) ? _arrowSR.sortingOrder : coinOrder - 1;
        int overlayOrder = Mathf.Min(coinOrder - 2, arrowOrder - 1);

        _maskSR.sortingLayerID = _coinSR ? _coinSR.sortingLayerID : 0;
        _maskSR.sortingOrder = overlayOrder;
        _maskSR.material = _maskMat;

        var cam = _worldCam ? _worldCam : Camera.main;
        if (cam && cam.orthographic)
        {
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            _maskGO.transform.localPosition = new Vector3(0f, 0f, 0f);
            _maskGO.transform.localScale = new Vector3(w, h, 1f);
        }
        else
        {
            _maskGO.transform.localScale = new Vector3(100f, 100f, 1f);
        }

        _maskMat.SetColor(_propColor, new Color(maskColor.r, maskColor.g, maskColor.b, maskOpacity));
        _maskMat.SetVector(_propRadius, maskStretch);
        _maskMat.SetFloat(_propFeather, Mathf.Clamp01(maskFeather));

        _maskGO.SetActive(true);
    }

    void UpdateIsolationMaskVisibility(bool forceHidden)
    {
        if (_maskGO == null) return;
        _maskGO.SetActive(!forceHidden);
    }

    void UpdateIsolationMask()
    {
        if (_maskMat == null) return;

        if (_maskSR && _coinSR)
        {
            int coinOrder = _coinSR.sortingOrder;
            int arrowOrder = (_arrowSR != null) ? _arrowSR.sortingOrder : coinOrder - 1;
            int overlayOrder = Mathf.Min(coinOrder - 2, arrowOrder - 1);
            _maskSR.sortingLayerID = _coinSR.sortingLayerID;
            _maskSR.sortingOrder = overlayOrder;
        }

        _maskMat.SetColor(_propColor, new Color(maskColor.r, maskColor.g, maskColor.b, maskOpacity));
        _maskMat.SetVector(_propRadius, new Vector2(Mathf.Abs(maskStretch.x), Mathf.Abs(maskStretch.y)));
        _maskMat.SetFloat(_propFeather, Mathf.Clamp01(maskFeather));

        var cam = _worldCam ? _worldCam : Camera.main;
        Vector3 world = GetProbeWorld();
        if (cam)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            _maskMat.SetVector(_propCenter, new Vector2(vp.x, vp.y));
        }
        else
        {
            _maskMat.SetVector(_propCenter, new Vector2(0.5f, 0.5f));
        }

        if (_maskGO && cam && cam.orthographic)
        {
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            _maskGO.transform.localScale = new Vector3(w, h, 1f);
        }
    }

    void DestroyIsolationMask()
    {
        if (_maskGO) Destroy(_maskGO);
        _maskGO = null;
        _maskSR = null;
        if (_maskMat) Destroy(_maskMat);
        _maskMat = null;
        if (_maskTex) Destroy(_maskTex);
        _maskTex = null;
    }
}
