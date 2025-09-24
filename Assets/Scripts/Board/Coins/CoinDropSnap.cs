using System.Collections;
using System.Linq;
using Mirror;
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
    public AnimationCurve snapEase;

    [Header("Networking during tween")]
    public bool sendNetworkDuringTween = true;

    [Header("Lock after placement")]
    public bool lockCoinAfterPlacement = false;

    // ====== runtime state ======
    Coroutine _snapRoutine;
    Vector3 _lastValidWorldPos;
    float _spawnZ;

    ValidDropSpot _occupiedSpot; // purely local pointer for convenience
    CoinDragSync _sync;          // optional: to stream position during tween

    void Awake()
    {
        _sync = GetComponent<CoinDragSync>();
    }

    void Start()
    {
        // Remember initial "valid" world pos for fallback
        SetLastValidWorldPos(transform.position, alsoSetZ: true);
    }

    /// <summary>
    /// Called by your drag handler once the user releases the coin.
    /// This method only adds logging; snap/claim flow remains the same.
    /// </summary>
    public void OnDrop()
    {
        // World center around the coin
        Vector2 center2D = new Vector2(transform.position.x, transform.position.y);

        // Find potential colliders in a small radius (layer-filtered)
        var hitsAll = Physics2D.OverlapCircleAll(center2D, overlapRadius, validSpotLayers);
        var hits = hitsAll?.Where(h => h != null).ToArray();

        // Map to ValidDropSpot components and filter by ContainsPoint & enabledForPlacement
        var spots = hits?
            .Select(h => h.GetComponentInParent<ValidDropSpot>() ?? h.GetComponent<ValidDropSpot>())
            .Where(s => s != null && s.enabledForPlacement && s.ContainsPoint(center2D))
            .Distinct()
            .ToList();

        Debug.Log($"[DROP] hits={hits?.Length ?? 0}  spots={spots?.Count ?? 0} pos={transform.position}");

        if (spots != null)
        {
            foreach (var s in spots)
                Debug.Log($"[DROP] candidate spot idx={s.spotIndex} enabled={s.enabledForPlacement} occupied={s.isOccupied} hasCollider={s.TryGetComponent<Collider2D>(out _)} center={s.GetCenterWorld()}");
        }

        // No valid spots? Snap back.
        if (spots == null || spots.Count == 0)
        {
            Vector3 back = _lastValidWorldPos;
            if (!keepCurrentZ) back.z = _spawnZ;
            StartSnapTween(back, updateLastValid: false);
            return;
        }

        // Choose the closest by center
        var best = spots.OrderBy(s => Vector2.SqrMagnitude(center2D - (Vector2)s.GetCenterWorld())).First();
        Debug.Log($"[DROP] best idx={best.spotIndex} enabled={best.enabledForPlacement} occupied={best.isOccupied}");

        // Networked claim path
        var netId = GetComponent<NetworkIdentity>();
        var board = BoardSpotsNet.Instance;

        if (netId != null && board != null)
        {
            if (_snapRoutine != null) { StopCoroutine(_snapRoutine); _snapRoutine = null; }

            Debug.Log($"[DROP] RequestClaim spot={best.spotIndex} coin={(netId ? netId.netId.ToString() : "noNetId")}");

            board.RequestClaim(best.spotIndex, netId, (ok, center) =>
            {
                Debug.Log($"[DROP] ClaimResult ok={ok} spot={best.spotIndex} center={center}");
                if (ok)
                {
                    Vector3 target = center;
                    if (keepCurrentZ) target.z = transform.position.z;

                    _occupiedSpot = best; // local note (server already marked it)
                    StartSnapTween(target, updateLastValid: true);

                    if (lockCoinAfterPlacement)
                    {
                        var guard = GetComponent<CoinPlacedLock>();
                        if (guard) guard.Lock();
                    }
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

        // Offline / no board fallback (pure local)
        Debug.LogWarning("[DROP] No BoardSpotsNet or NetworkIdentity on coin; using offline fallback.");
        best.ForceOccupy(gameObject);
        Vector3 targetOffline = best.GetCenterWorld();
        if (keepCurrentZ) targetOffline.z = transform.position.z;
        _occupiedSpot = best;
        StartSnapTween(targetOffline, updateLastValid: true);
    }

    // ====== Tweening / helpers (unchanged semantics, only emits debug if helpful) ======

    void StartSnapTween(Vector3 target, bool updateLastValid)
    {
        if (_snapRoutine != null) StopCoroutine(_snapRoutine);
        _snapRoutine = StartCoroutine(SnapTo(target, updateLastValid));
    }

    IEnumerator SnapTo(Vector3 target, bool updateLastValid)
    {
        Vector3 start = transform.position;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, snapDuration);

        while (t < dur)
        {
            float p = t / dur;
            float eased = (snapEase != null) ? snapEase.Evaluate(p) : p;

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
            SetLastValidWorldPos(target, alsoSetZ: !keepCurrentZ);
        }

        _snapRoutine = null;
    }

    void SetLastValidWorldPos(Vector3 worldPos, bool alsoSetZ)
    {
        _lastValidWorldPos = worldPos;
        if (alsoSetZ) _spawnZ = worldPos.z;
    }

    /// <summary>
    /// Optional helper if you manually want to unlock & clear (kept for parity).
    /// </summary>
    public void ReleasePlacementLockAndSpot()
    {
        if (_occupiedSpot != null)
        {
            // Intentionally not changing network logic here; this is purely local bookkeeping.
            _occupiedSpot = null;
        }
        var lockGuard = GetComponent<CoinPlacedLock>();
        if (lockGuard != null) lockGuard.Unlock();
    }
}
