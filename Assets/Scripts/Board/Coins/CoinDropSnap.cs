using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
public class CoinDropSnap : MonoBehaviour
{
    [Header("Detection")]
    public float overlapRadius = 0.05f;
    public LayerMask validSpotLayers = ~0;

    [Header("Z Handling")]
    public bool keepCurrentZ = true;

    [Header("Snap Tween")]
    public float snapDuration = 0.18f;
    public AnimationCurve snapEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool sendNetworkDuringTween = true;

    [Header("Placement Rules")]
    [Tooltip("When true, once placed on a valid spot the coin is locked and cannot be picked up again.")]
    public bool lockCoinAfterPlacement = true;

    Vector3 _lastValidWorldPos;
    float _spawnZ;
    CoinDragHandler _drag;
    CoinDragSync _sync;
    Coroutine _snapRoutine;
    ValidDropSpot _occupiedSpot;

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
        if (_snapRoutine != null)
        {
            StopCoroutine(_snapRoutine);
            _snapRoutine = null;
        }
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

            if (best.TryOccupy(gameObject))
            {
                Vector3 target = best.GetCenterWorld();
                if (keepCurrentZ) target.z = transform.position.z;

                _occupiedSpot = best;
                StartSnapTween(target, updateLastValid: true);

                if (lockCoinAfterPlacement)
                {
                    var lockGuard = GetComponent<CoinPlacedLock>();
                    if (lockGuard != null) lockGuard.Lock();
                }
                return;
            }
        }

        Vector3 back = _lastValidWorldPos;
        if (!keepCurrentZ) back.z = _spawnZ;
        StartSnapTween(back, updateLastValid: false);
    }

    void StartSnapTween(Vector3 target, bool updateLastValid)
    {
        if (_snapRoutine != null)
        {
            StopCoroutine(_snapRoutine);
        }
        _snapRoutine = StartCoroutine(SnapTweenRoutine(target, updateLastValid));
    }

    IEnumerator SnapTweenRoutine(Vector3 target, bool updateLastValid)
    {
        Vector3 start = transform.position;
        if (snapDuration <= 0.0001f)
        {
            transform.position = target;
            if (_sync) _sync.OwnerSnapTo(target);
            if (updateLastValid) _lastValidWorldPos = target;
            yield break;
        }

        float t = 0f;
        while (t < snapDuration)
        {
            float p = t / snapDuration;
            float eased = snapEase != null ? snapEase.Evaluate(p) : p;

            Vector3 pos = Vector3.LerpUnclamped(start, target, eased);
            transform.position = pos;

            if (sendNetworkDuringTween && _sync != null)
            {
                _sync.OwnerSendPositionThrottled(pos);
            }

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = target;

        if (_sync != null)
        {
            _sync.OwnerSnapTo(target);
        }

        if (updateLastValid)
        {
            _lastValidWorldPos = target;
        }

        _snapRoutine = null;
    }

    public void SetHome(Vector3 worldPos, bool alsoSetZ = true)
    {
        _lastValidWorldPos = worldPos;
        if (alsoSetZ) _spawnZ = worldPos.z;
    }

    public void ReleasePlacementLockAndSpot()
    {
        if (_occupiedSpot != null)
        {
            _occupiedSpot.Release(gameObject);
            _occupiedSpot = null;
        }
        var lockGuard = GetComponent<CoinPlacedLock>();
        if (lockGuard != null) lockGuard.Unlock();
    }
}