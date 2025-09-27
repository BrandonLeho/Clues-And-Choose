using System.Collections.Generic;
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

    [Header("Local Spotlight Mask")]
    public Vector2 maskScale = new Vector2(2.0f, 1.2f);
    public Vector2 maskOffsetLocal = Vector2.zero;
    public float maskZOffset = -0.01f;

    [Header("Feathered Soft Mask")]
    public bool useFeatheredSoftMask = true;
    public Material softMaskMaterial;
    [Min(0f)] public float softFeather = 0.2f;

    [Header("Hard SpriteMask")]
    public bool enableSpotlightMask = false;
    public Sprite capsuleMaskSprite;
    public int maskBackSortingOrderBias = -10000;
    public int maskFrontSortingOrderBias = 10000;
    public bool maskAlignToArrow = true;
    public float maskAutoLengthPerUnit = 0.6f;

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

    SpriteMask _spotlightMask;

    struct Touched
    {
        public SpriteRenderer sr;
        public SpriteMaskInteraction prevMask;
        public Material prevMat;
    }
    readonly List<Touched> _touched = new();

    Vector3 _capA_World, _capB_World;
    float _capRadiusWorld, _capFeatherWorld;

    static readonly int PID_A = Shader.PropertyToID("_SoftCapA");
    static readonly int PID_B = Shader.PropertyToID("_SoftCapB");
    static readonly int PID_R = Shader.PropertyToID("_SoftCapRadius");
    static readonly int PID_F = Shader.PropertyToID("_SoftCapFeather");
    static readonly int PID_Enable = Shader.PropertyToID("_SoftCapEnable");
    static MaterialPropertyBlock _mpb;

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

        if (_drag)
        {
            _drag.onPickUp.AddListener(OnPickUp);
            _drag.onDrop.AddListener(OnDrop);
        }
        if (_netDrag) _netDrag.DragStateChanged += OnNetDragStateChanged;

        if (!gridMask)
        {
            var found = GameObject.Find("ColorGrid");
            if (found) gridMask = found.GetComponent<RectTransform>();
        }

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void OnDestroy()
    {
        if (_drag)
        {
            _drag.onPickUp.RemoveListener(OnPickUp);
            _drag.onDrop.RemoveListener(OnDrop);
        }
        if (_netDrag) _netDrag.DragStateChanged -= OnNetDragStateChanged;

        TeardownSpotlight();
        ApplyMaskToOtherCoins(enable: false);
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

        if (showAsLocal) Active = this;

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

        if (showAsLocal)
        {
            if (useFeatheredSoftMask)
            {
                ApplyMaskToOtherCoins(enable: true);
            }
            else if (enableSpotlightMask)
            {
                SetupSpotlight();
                ApplyMaskToOtherCoins(enable: true);
            }
        }
    }

    void StopArrow(bool isLocalCall)
    {
        _isDragging = false;

        if (isLocalCall && Active == this) Active = null;

        if (_arrowInst) Destroy(_arrowInst.gameObject);
        _arrowInst = null;
        _arrowSR = null;
        _tipGraphic = null;

        _suppressUntilInside = false;
        _animT = 0f;
        _animating = false;
        _targetShown = false;
        _remoteMode = false;

        TeardownSpotlight();
        ApplyMaskToOtherCoins(enable: false);
    }

    void Update()
    {
        if (!_isDragging || !_arrowInst) { TickArrowAnimatorOnlyHide(); return; }

        _arrowInst.localPosition = new Vector3(arrowOffsetLocal.x, arrowOffsetLocal.y, arrowLocalZ);

        SyncArrowSortingLayerAndOrder();
        UpdateTipLagRotation();

        bool inside = IsProbeInsideGrid();
        if (_suppressUntilInside)
        {
            if (inside) { _suppressUntilInside = false; SetArrowShown(true); }
            else SetArrowShown(false);
        }
        else SetArrowShown(inside);

        TickArrowAnimator();

        if (Active == this)
        {
            UpdateSoftCapsuleWorldParams();
            if (_spotlightMask) UpdateSpotlightPose();
            if (useFeatheredSoftMask) PushSoftParamsToTouched();
        }
    }

    void TickArrowAnimatorOnlyHide()
    {
        if (_arrowInst == null) return;
        TickArrowAnimator();
    }

    void SyncArrowSortingLayerAndOrder()
    {
        if (!alignSortingWithCoin || _coinSR == null || _arrowSR == null) return;
        _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
        _arrowSR.sortingOrder = forceArrowBelow
            ? _coinSR.sortingOrder - Mathf.Max(1, arrowBelowOffset)
            : _coinSR.sortingOrder;
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

        if (_spotlightMask) _spotlightMask.gameObject.SetActive(shown);
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
            if (_arrowInst.gameObject.activeSelf) _arrowInst.gameObject.SetActive(false);
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

    void UpdateSoftCapsuleWorldParams()
    {
        Vector2 localA = Vector2.zero;
        Vector2 localB = arrowOffsetLocal;

        Vector2 mid = (localA + localB) * 0.5f + maskOffsetLocal;

        Vector2 dir = localB - localA;
        float len = dir.magnitude;
        Vector2 ndir = len > 1e-5f ? dir / len : new Vector2(1f, 0f);

        float radiusLocal = Mathf.Max(0.0001f, maskScale.y * 0.5f);
        float halfLenLocal = Mathf.Max(0f, (maskScale.x - maskScale.y) * 0.5f);

        Vector2 capALocal = mid - ndir * halfLenLocal;
        Vector2 capBLocal = mid + ndir * halfLenLocal;

        Vector3 A = transform.TransformPoint(capALocal.x, capALocal.y, 0f);
        Vector3 B = transform.TransformPoint(capBLocal.x, capBLocal.y, 0f);

        var ls = transform.lossyScale;
        float planarScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
        float R = radiusLocal * planarScale;

        _capA_World = A;
        _capB_World = B;
        _capRadiusWorld = R;
        _capFeatherWorld = Mathf.Max(0f, softFeather);
    }

    void SetupSpotlight()
    {
        if (!enableSpotlightMask || !capsuleMaskSprite) return;

        if (_spotlightMask == null)
        {
            var go = new GameObject("CoinSpotlightMask");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, 0f, maskZOffset);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            _spotlightMask = go.AddComponent<SpriteMask>();
            _spotlightMask.sprite = capsuleMaskSprite;
            _spotlightMask.isCustomRangeActive = true;

            if (_coinSR != null)
            {
                _spotlightMask.frontSortingLayerID = _coinSR.sortingLayerID;
                _spotlightMask.backSortingLayerID = _coinSR.sortingLayerID;
                _spotlightMask.frontSortingOrder = _coinSR.sortingOrder + maskFrontSortingOrderBias;
                _spotlightMask.backSortingOrder = _coinSR.sortingOrder + maskBackSortingOrderBias;
            }
        }

        _spotlightMask.gameObject.SetActive(true);
        UpdateSpotlightPose();
    }

    void UpdateSpotlightPose()
    {
        if (_spotlightMask == null) return;

        Vector2 localA = Vector2.zero;
        Vector2 localB = arrowOffsetLocal;
        Vector2 mid = (localA + localB) * 0.5f + maskOffsetLocal;

        float dist = (localB - localA).magnitude;
        float sx = Mathf.Max(0.01f, maskScale.x + dist * Mathf.Max(0f, maskAutoLengthPerUnit));
        float sy = Mathf.Max(0.01f, maskScale.y);

        _spotlightMask.transform.localPosition = new Vector3(mid.x, mid.y, maskZOffset);

        if (maskAlignToArrow)
        {
            float ang = Mathf.Atan2(localB.y - localA.y, localB.x - localA.x) * Mathf.Rad2Deg;
            _spotlightMask.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
        else
        {
            _spotlightMask.transform.localRotation = Quaternion.identity;
        }

        _spotlightMask.transform.localScale = new Vector3(sx, sy, 1f);
    }

    void TeardownSpotlight()
    {
        if (_spotlightMask)
        {
            Destroy(_spotlightMask.gameObject);
            _spotlightMask = null;
        }
    }

    void ApplyMaskToOtherCoins(bool enable)
    {
        if (!enable)
        {
            for (int i = 0; i < _touched.Count; i++)
            {
                var t = _touched[i];
                if (t.sr)
                {
                    t.sr.maskInteraction = t.prevMask;

                    if (t.prevMat) t.sr.material = t.prevMat;

                    _mpb.Clear();
                    _mpb.SetFloat(PID_Enable, 0f);
                    t.sr.SetPropertyBlock(_mpb);
                }
            }
            _touched.Clear();
            return;
        }

        var allCoins = FindObjectsByType<CoinDragHandler>(FindObjectsSortMode.None);
        for (int i = 0; i < allCoins.Length; i++)
        {
            var coin = allCoins[i];
            if (!coin) continue;
            if (coin.gameObject == this.gameObject) continue;

            var srs = coin.GetComponentsInChildren<SpriteRenderer>(true);
            for (int j = 0; j < srs.Length; j++)
            {
                var sr = srs[j];
                if (!sr) continue;

                bool already = false;
                for (int k = 0; k < _touched.Count; k++)
                    if (_touched[k].sr == sr) { already = true; break; }
                if (already) continue;

                var t = new Touched
                {
                    sr = sr,
                    prevMask = sr.maskInteraction,
                    prevMat = sr.sharedMaterial
                };

                if (useFeatheredSoftMask)
                {
                    if (softMaskMaterial)
                    {
                        sr.material = softMaskMaterial;
                    }
                }
                else
                {
                    sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                }

                _touched.Add(t);
            }
        }
    }

    void PushSoftParamsToTouched()
    {
        if (!useFeatheredSoftMask || _touched.Count == 0) return;

        for (int i = 0; i < _touched.Count; i++)
        {
            var sr = _touched[i].sr;
            if (!sr) continue;

            _mpb.Clear();
            _mpb.SetVector(PID_A, _capA_World);
            _mpb.SetVector(PID_B, _capB_World);
            _mpb.SetFloat(PID_R, _capRadiusWorld);
            _mpb.SetFloat(PID_F, _capFeatherWorld);
            _mpb.SetFloat(PID_Enable, 1f);
            sr.SetPropertyBlock(_mpb);
        }
    }
}
