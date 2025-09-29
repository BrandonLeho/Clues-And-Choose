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
    public bool enableSpotlightMask = true;
    public Sprite capsuleMaskSprite;
    public Vector2 maskScale = new Vector2(2.0f, 1.2f);
    public Vector2 maskOffsetLocal = Vector2.zero;
    public float maskZOffset = -0.01f;
    public int maskBackSortingOrderBias = -10000;
    public int maskFrontSortingOrderBias = 10000;
    public bool maskAlignToArrow = true;
    public float maskAutoLengthPerUnit = 0.6f;

    [Header("Mask Target Filtering")]
    public bool hideOtherCoins = true;
    public LayerMask layersToHide;

    [Header("Mask Refresh")]
    [Range(0.05f, 0.5f)] public float maskRescanInterval = 0.15f;

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

    CoinDragSync _netDrag;

    SpriteMask _spotlightMask;
    readonly List<(SpriteRenderer sr, SpriteMaskInteraction prev)> _touched = new();

    static bool _globalRefreshRequested;
    float _maskRefreshTimer;
    bool _pendingRefresh;

    public bool IsDraggingForMask => _isDragging;


    public static void RequestMaskRefreshGlobal() => _globalRefreshRequested = true;

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

        if (_netDrag)
        {
            _netDrag.DragStateChanged += OnNetDragStateChanged;
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
        if (_netDrag)
        {
            _netDrag.DragStateChanged -= OnNetDragStateChanged;
        }
        TeardownSpotlight();
    }

    void OnPickUp()
    {
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
            StartArrow(showAsLocal: false);
            RequestMaskRefreshGlobal();
        }
        else
        {
            StopArrow(isLocalCall: false);
            RequestMaskRefreshGlobal();
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
            _arrowInst.gameObject.SetActive(true);
            ApplyArrowPose();
        }

        if (showAsLocal && enableSpotlightMask)
        {
            SetupSpotlight();
            EnsureMaskSortingCoverage();
            ApplyMaskToOtherCoins(enable: true);
            _maskRefreshTimer = maskRescanInterval;
        }

        if (!showAsLocal) RequestMaskRefreshGlobal();
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
        _targetShown = false;

        TeardownSpotlight();
        ApplyMaskToOtherCoins(enable: false);
    }

    void Update()
    {
        if (!_isDragging || !_arrowInst) { TickArrowAnimatorOnlyHide(); }
        else
        {
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
        }

        if (_spotlightMask && Active == this)
        {
            EnsureMaskSortingCoverage();

            if (_globalRefreshRequested) { _globalRefreshRequested = false; _pendingRefresh = true; }

            _maskRefreshTimer -= Time.unscaledDeltaTime;
            if (_pendingRefresh || _maskRefreshTimer <= 0f)
            {
                _pendingRefresh = false;
                _maskRefreshTimer = Mathf.Max(0.05f, maskRescanInterval);
                ApplyMaskToOtherCoins(enable: true);
            }
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

        if (_spotlightMask)
            _spotlightMask.gameObject.SetActive(shown);
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
            ApplyArrowPose();
        }

        if (Mathf.Approximately(_animT, 0f) && !_targetShown)
        {
            ApplyArrowPose();
            if (_arrowInst.gameObject.activeSelf) _arrowInst.gameObject.SetActive(false);
        }

        if (Mathf.Approximately(_animT, 1f) && _targetShown)
        {
            ApplyArrowPose();
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

    void SetupSpotlight()
    {
        if (!capsuleMaskSprite) return;
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
            }
        }

        _spotlightMask.gameObject.SetActive(true);
        EnsureMaskSortingCoverage();
        UpdateSpotlightPose();
    }

    void EnsureMaskSortingCoverage()
    {
        if (_spotlightMask == null) return;

        if (_coinSR != null)
        {
            _spotlightMask.frontSortingLayerID = _coinSR.sortingLayerID;
            _spotlightMask.backSortingLayerID = _coinSR.sortingLayerID;
        }

        _spotlightMask.isCustomRangeActive = true;

        _spotlightMask.backSortingOrder = -10000;
        _spotlightMask.frontSortingOrder = 10000;
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
                var entry = _touched[i];
                if (entry.sr) entry.sr.maskInteraction = entry.prev;
            }
            _touched.Clear();
            return;
        }

        if (_spotlightMask == null) return;

        void Touch(SpriteRenderer sr)
        {
            if (!sr) return;
            for (int k = 0; k < _touched.Count; k++)
                if (_touched[k].sr == sr) return;

            _touched.Add((sr, sr.maskInteraction));
            sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        }

        if (hideOtherCoins)
        {
            var allCoins = FindObjectsByType<CoinDragHandler>(FindObjectsSortMode.None);
            for (int i = 0; i < allCoins.Length; i++)
            {
                var coin = allCoins[i];
                if (!coin) continue;
                if (coin.gameObject == gameObject) continue;

                var otherProbe = coin.GetComponent<CoinPlacementProbe>();
                var otherDrop = coin.GetComponent<CoinDropSnap>();
                var otherLock = coin.GetComponent<CoinPlacedLock>();

                bool isLocked = otherLock && otherLock.locked;
                bool isLanding = otherDrop && otherDrop.IsLanding;
                bool isDragging = otherProbe && otherProbe.IsDraggingForMask;

                if (!isDragging || isLocked || isLanding)
                    continue;

                var srs = coin.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
                for (int j = 0; j < srs.Length; j++)
                    Touch(srs[j]);
            }
        }

        if (layersToHide.value != 0)
        {
            var allSRs = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < allSRs.Length; i++)
            {
                var sr = allSRs[i];
                if (!sr) continue;

                if (sr.transform.IsChildOf(this.transform)) continue;

                int mask = 1 << sr.gameObject.layer;
                if ((layersToHide.value & mask) != 0)
                    Touch(sr);
            }
        }
    }
}
