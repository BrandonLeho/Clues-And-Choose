using System.Linq;
using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
public class CoinDropSnap : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Physics2D overlap is run at the coin's center. If none match, coin snaps back.")]
    public float overlapRadius = 0.05f;

    [Tooltip("Optional: restrict detection to these layers (leave empty to detect all).")]
    public LayerMask validSpotLayers = ~0;

    [Header("Z Handling")]
    [Tooltip("Keep the coin's current Z when snapping. If false, use the drop spot's Z.")]
    public bool keepCurrentZ = true;

    Vector3 _lastValidWorldPos;
    float _spawnZ;
    CoinDragHandler _drag;
    CoinDragSync _sync;

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        _sync = GetComponent<CoinDragSync>();

        _drag.onPickUp.AddListener(OnPickUp);
        _drag.onDrop.AddListener(OnDrop);
    }

    void Start()
    {
        _lastValidWorldPos = transform.position;
        _spawnZ = transform.position.z;
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

    }

    void OnDrop()
    {
        Vector2 center2D = new Vector2(transform.position.x, transform.position.y);

        var hits = Physics2D.OverlapCircleAll(center2D, overlapRadius, validSpotLayers);

        var spots = hits?
            .Select(h => h.GetComponentInParent<ValidDropSpot>() ?? h.GetComponent<ValidDropSpot>())
            .Where(s => s != null && s.enabledForPlacement && s.ContainsPoint(center2D))
            .ToList();

        if (spots != null && spots.Count > 0)
        {
            var best = spots
                .OrderBy(s => Vector2.SqrMagnitude(center2D - (Vector2)s.GetCenterWorld()))
                .First();

            Vector3 snapTarget = best.GetCenterWorld();
            if (keepCurrentZ) snapTarget.z = transform.position.z;

            ApplySnap(snapTarget, isValid: true);
        }
        else
        {
            Vector3 back = _lastValidWorldPos;
            if (!keepCurrentZ) back.z = _spawnZ;
            ApplySnap(back, isValid: false);
        }
    }

    void ApplySnap(Vector3 target, bool isValid)
    {
        transform.position = target;

        if (isValid) _lastValidWorldPos = target;

        if (_sync != null)
        {
            _sync.OwnerSnapTo(target);
        }
    }
}
