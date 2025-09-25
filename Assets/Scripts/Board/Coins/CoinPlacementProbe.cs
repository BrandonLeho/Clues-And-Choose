using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
[DisallowMultipleComponent]
public class CoinPlacementProbe : MonoBehaviour
{
    public static CoinPlacementProbe Active { get; private set; }
    public static bool ProbeMode => Active != null;

    public bool offsetIsLocal = true;
    public Vector2 probeOffsetLocal = new Vector2(0f, -0.6f);
    public Vector2 probeOffsetWorld = Vector2.zero;
    public bool overrideWithWorldOffset = false;
    public Camera uiCamera;

    public Transform arrowPrefab;
    public bool linkArrowToProbe = true;
    public bool rotateArrowToProbe = true;
    public Vector2 arrowVisualLocalOffset = new Vector2(0f, -0.4f);
    public float arrowZOffset = 0f;
    [Range(1f, 40f)] public float arrowFollowLerp = 20f;
    public bool alignSortingWithCoin = true;
    public int arrowSortingOrderDelta = +1;

    public bool gizmoShowProbe = true;
    public Color gizmoColor = new Color(0.2f, 1f, 0.6f, 0.9f);
    public float gizmoSize = 0.06f;
    public bool debugLogs = false;

    CoinDragHandler _drag;
    Transform _arrowInst;
    SpriteRenderer _arrowSR;
    SpriteRenderer _coinSR;
    bool _isDragging;

    public Vector3 GetProbeWorld()
    {
        var basePos = transform.position;
        return overrideWithWorldOffset || !offsetIsLocal
            ? basePos + (Vector3)probeOffsetWorld
            : transform.TransformPoint((Vector3)probeOffsetLocal);
    }

    public Vector2 GetProbeScreenPosition()
    {
        var cam = uiCamera ? uiCamera : Camera.main;
        if (!cam) return GetProbeWorld();
        return cam.WorldToScreenPoint(GetProbeWorld());
    }

    public void SetProbeOffsetLocal(Vector2 local) => probeOffsetLocal = local;
    public void SetProbeOffsetWorld(Vector2 world) => probeOffsetWorld = world;
    public void NudgeProbeLocal(Vector2 delta) => probeOffsetLocal += delta;

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        _coinSR = GetComponent<SpriteRenderer>();
        if (_drag)
        {
            _drag.onPickUp.AddListener(OnPickUp);
            _drag.onDrop.AddListener(OnDrop);
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
        _isDragging = true;
        Active = this;

        if (arrowPrefab)
        {
            _arrowInst = Instantiate(arrowPrefab, transform);
            _arrowSR = _arrowInst.GetComponentInChildren<SpriteRenderer>();
            if (alignSortingWithCoin && _coinSR && _arrowSR)
            {
                _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
                _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
            }
            ApplyArrowTransformImmediate();
        }

        if (debugLogs) Debug.Log($"[{name}] Probe OnPickUp() active, linkArrowToProbe={linkArrowToProbe}");
    }

    void OnDrop()
    {
        _isDragging = false;
        if (Active == this) Active = null;

        if (_arrowInst) Destroy(_arrowInst.gameObject);
        _arrowInst = null;
        _arrowSR = null;

        if (debugLogs) Debug.Log($"[{name}] Probe OnDrop()");
    }

    void Update()
    {
        if (!_isDragging || !_arrowInst) return;

        Vector3 targetPos = linkArrowToProbe
            ? GetProbeWorld()
            : transform.TransformPoint((Vector3)arrowVisualLocalOffset);

        targetPos.z = transform.position.z + arrowZOffset;
        _arrowInst.position = Vector3.Lerp(
            _arrowInst.position,
            targetPos,
            1f - Mathf.Exp(-arrowFollowLerp * Time.deltaTime));

        if (rotateArrowToProbe && linkArrowToProbe)
        {
            Vector2 dir = (overrideWithWorldOffset || !offsetIsLocal)
                ? (probeOffsetWorld.sqrMagnitude > 0.00001f ? probeOffsetWorld : Vector2.down)
                : (probeOffsetLocal.sqrMagnitude > 0.00001f ? probeOffsetLocal : Vector2.down);

            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _arrowInst.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        if (alignSortingWithCoin && _coinSR && _arrowSR)
        {
            _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
            _arrowSR.sortingOrder = _coinSR.sortingOrder + Mathf.Max(1, arrowSortingOrderDelta);
        }
    }

    void ApplyArrowTransformImmediate()
    {
        if (!_arrowInst) return;

        Vector3 p = linkArrowToProbe
            ? GetProbeWorld()
            : transform.TransformPoint((Vector3)arrowVisualLocalOffset);

        p.z = transform.position.z + arrowZOffset;
        _arrowInst.position = p;

        if (rotateArrowToProbe && linkArrowToProbe)
        {
            Vector2 dir = (overrideWithWorldOffset || !offsetIsLocal)
                ? (probeOffsetWorld.sqrMagnitude > 0.00001f ? probeOffsetWorld : Vector2.down)
                : (probeOffsetLocal.sqrMagnitude > 0.00001f ? probeOffsetLocal : Vector2.down);

            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _arrowInst.rotation = Quaternion.Euler(0f, 0f, ang);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!gizmoShowProbe) return;
        Gizmos.color = gizmoColor;

        Vector3 probe = Application.isPlaying
            ? GetProbeWorld()
            : (overrideWithWorldOffset || !offsetIsLocal)
                ? transform.position + (Vector3)probeOffsetWorld
                : transform.TransformPoint((Vector3)probeOffsetLocal);

        Gizmos.DrawSphere(probe, gizmoSize);
        Gizmos.DrawLine(transform.position, probe);
    }
}
