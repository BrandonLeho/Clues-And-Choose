using System.Collections;
using System.Linq;
using UnityEngine;
using Mirror;

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
            .Where(s => s != null && s.enabledForPlacement)
            .ToList();

        Debug.Log($"[DropCheck] hits={hits?.Length ?? 0} spotsAfterFilter={spots?.Count ?? 0}");
        if (spots != null)
        {
            foreach (var s in spots)
                Debug.Log($"[DropCheck] spot={s.name} enabled={s.enabledForPlacement} idx={s.spotIndex}");
        }


        if (spots != null && spots.Count > 0)
        {
            var best = spots.OrderBy(s => Vector2.SqrMagnitude(center2D - (Vector2)s.GetCenterWorld())).First();
            int idx = best.spotIndex;

            var ni = GetComponent<NetworkIdentity>();
            if (!ni)
            {
                best.SetOccupantLocal(gameObject);
                StartSnapTween(best.GetCenterWorld(), updateLastValid: true);
                GetComponent<CoinPlacedLock>()?.Lock();
                return;
            }

            BoardSpotsNet.RequestClaim(idx, ni, (ok, snapWorld) =>
            {
                if (ok)
                {
                    if (keepCurrentZ) snapWorld.z = transform.position.z;
                    StartSnapTween(snapWorld, updateLastValid: true);
                    GetComponent<CoinPlacedLock>()?.Lock();
                }
                else
                {
                    Vector3 back = _lastValidWorldPos;
                    if (!keepCurrentZ) back.z = _spawnZ;
                    StartSnapTween(back, updateLastValid: false);
                }
            });
            return;
        }

        Vector3 backPos = _lastValidWorldPos;
        if (!keepCurrentZ) backPos.z = _spawnZ;
        StartSnapTween(backPos, updateLastValid: false);
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

    IEnumerator ClaimAndSnapNetworked(ValidDropSpot best, ValidDropSpotNet net)
    {
        bool got = false, ok = false;
        Vector3 snapPos = Vector3.zero;

        System.Action<bool, Vector3> cb = (success, pos) =>
        {
            got = true; ok = success; snapPos = pos;
        };
        net.OnClientClaimResult = cb;
        net.CmdRequestClaim(gameObject);

        float t = 0f, timeout = 1.0f;
        while (!got && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (got && ok)
        {
            Vector3 target = snapPos;
            if (keepCurrentZ) target.z = transform.position.z;

            var lockGuard = GetComponent<CoinPlacedLock>();
            if (lockGuard != null) lockGuard.Lock();

            StartSnapTween(target, updateLastValid: true);
        }
        else
        {
            Vector3 back = _lastValidWorldPos;
            if (!keepCurrentZ) back.z = _spawnZ;
            StartSnapTween(back, updateLastValid: false);
        }
    }
}
