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
    public bool gizmoShowProbe = true;
    public Color gizmoColor = new Color(0.2f, 1f, 0.6f, 0.9f);
    public float gizmoSize = 0.06f;

    CoinDragHandler _drag;
    Transform _arrowInst;
    SpriteRenderer _arrowSR;
    SpriteRenderer _coinSR;
    bool _isDragging;

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
            ApplyArrowLocalTransformImmediate();
        }
    }

    void OnDrop()
    {
        _isDragging = false;
        if (Active == this) Active = null;
        if (_arrowInst) Destroy(_arrowInst.gameObject);
        _arrowInst = null;
        _arrowSR = null;
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

    void OnDrawGizmosSelected()
    {
        if (!gizmoShowProbe) return;
        Gizmos.color = gizmoColor;
        Vector3 localProbe = new Vector3(probeOffsetLocal.x, probeOffsetLocal.y, 0f);
        Vector3 worldProbe = transform.TransformPoint(localProbe);
        Gizmos.DrawSphere(worldProbe, gizmoSize);
        Gizmos.DrawLine(transform.position, worldProbe);
    }
}
