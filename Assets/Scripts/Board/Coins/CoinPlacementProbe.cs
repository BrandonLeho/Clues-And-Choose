using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
[DisallowMultipleComponent]
public class CoinPlacementProbe : MonoBehaviour
{
    [Header("Arrow Visual")]
    public Transform arrowPrefab;
    public float arrowZOffset = 0f;
    [Range(1f, 40f)] public float arrowFollowLerp = 20f;
    public bool rotateArrowToProbe = true;
    public bool alignSortingWithCoin = true;
    public int arrowSortingOrderDelta = -1;

    [Header("Probe Settings")]
    public Vector2 probeOffsetLocal = new Vector2(0f, -0.6f);
    public bool offsetIsLocal = true;

    [Header("Gizmos")]
    public bool gizmoShowProbe = true;
    public Color gizmoColor = new Color(0.2f, 1f, 0.6f, 0.9f);
    public float gizmoSize = 0.06f;

    CoinDragHandler _drag;
    Transform _arrowInst;
    SpriteRenderer _arrowSR;
    SpriteRenderer _coinSR;
    bool _isDragging;

    public Vector3 GetProbeWorld()
    {
        Vector3 local = new Vector3(probeOffsetLocal.x, probeOffsetLocal.y, 0f);
        var basePos = transform.position;
        if (offsetIsLocal) return transform.TransformPoint(local);
        return basePos + local;
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
        if (arrowPrefab)
        {
            _arrowInst = Instantiate(arrowPrefab, transform);
            _arrowSR = _arrowInst.GetComponentInChildren<SpriteRenderer>();
            if (alignSortingWithCoin && _coinSR && _arrowSR)
            {
                _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
                _arrowSR.sortingOrder = _coinSR.sortingOrder + arrowSortingOrderDelta;
            }
            UpdateArrowImmediate();
        }
    }

    void OnDrop()
    {
        _isDragging = false;
        if (_arrowInst) Destroy(_arrowInst.gameObject);
        _arrowInst = null;
        _arrowSR = null;
    }

    void Update()
    {
        if (!_isDragging || !_arrowInst) return;

        Vector3 target = GetProbeWorld();
        target.z = transform.position.z + arrowZOffset;
        _arrowInst.position = Vector3.Lerp(
            _arrowInst.position,
            target,
            1f - Mathf.Exp(-arrowFollowLerp * Time.deltaTime));

        if (rotateArrowToProbe)
        {
            Vector2 dir = probeOffsetLocal.sqrMagnitude > 0.00001f
                ? probeOffsetLocal
                : Vector2.down;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _arrowInst.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        if (alignSortingWithCoin && _coinSR && _arrowSR)
        {
            _arrowSR.sortingLayerID = _coinSR.sortingLayerID;
            _arrowSR.sortingOrder = _coinSR.sortingOrder + arrowSortingOrderDelta;
        }
    }

    void UpdateArrowImmediate()
    {
        if (!_arrowInst) return;
        var p = GetProbeWorld();
        p.z = transform.position.z + arrowZOffset;
        _arrowInst.position = p;

        if (rotateArrowToProbe)
        {
            Vector2 dir = probeOffsetLocal.sqrMagnitude > 0.00001f
                ? probeOffsetLocal
                : Vector2.down;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _arrowInst.rotation = Quaternion.Euler(0f, 0f, ang);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!gizmoShowProbe) return;
        Gizmos.color = gizmoColor;
        Vector3 probe = Application.isPlaying ? GetProbeWorld()
                     : (offsetIsLocal
                        ? transform.TransformPoint((Vector3)probeOffsetLocal)
                        : transform.position + (Vector3)probeOffsetLocal);
        Gizmos.DrawSphere(probe, gizmoSize);
        Gizmos.DrawLine(transform.position, probe);
    }
}
