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
    public AnimationCurve snapEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool sendNetworkDuringTween = true;

    [Header("Placement Rules")]
    public bool lockCoinAfterPlacement = true;

    [Header("Impact Hop / Stretch / Squash")]
    public bool useHop = true;
    public float hopHeight = 0.08f;
    [Range(0f, 1f)] public float hopPeakT = 0.35f;
    public float fallStretchY = 1.18f;
    public float fallSquashX = 0.92f;
    public float fallStretchSensitivity = 12f;
    public float landSquashX = 1.12f;
    public float landSquashY = 0.88f;
    public float landSquashTime = 0.10f;
    public AnimationCurve landSquashEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("landing pulse")]
    public ImpactPulse landingPulse;
    public bool tryFindPulseOnMainCamera = true;


    Vector3 _lastValidWorldPos;
    float _spawnZ;
    CoinDragHandler _drag;
    CoinDragSync _sync;
    Coroutine _snapRoutine;
    ValidDropSpot _occupiedSpot;

    CoinPlacementProbe _probe;

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        _sync = GetComponent<CoinDragSync>();
        _probe = GetComponent<CoinPlacementProbe>();

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
        Vector3 probeWorld = (_probe != null) ? _probe.GetProbeWorld() : transform.position;
        Vector2 center2D = new Vector2(probeWorld.x, probeWorld.y);

        var visualCell = ArrowProbeHoverRouter.Current;
        if (visualCell != null)
        {
            var forcedSpot = visualCell.GetComponent<ValidDropSpot>();
            if (forcedSpot != null)
            {
                if (forcedSpot.enabledForPlacement)
                {
                    TryClaimAndSnap(forcedSpot);
                }
                else
                {
                    RejectToLastValid();
                }
                return;
            }
        }
        var hits = Physics2D.OverlapCircleAll(center2D, overlapRadius, validSpotLayers);
        var spots = hits?
            .Select(h => h.GetComponentInParent<ValidDropSpot>() ?? h.GetComponent<ValidDropSpot>())
            .Where(s => s != null && s.enabledForPlacement && s.ContainsPoint(center2D))
            .ToList();

        if (spots != null && spots.Count > 0)
        {
            var best = spots.OrderBy(s => Vector2.SqrMagnitude(center2D - (Vector2)s.GetCenterWorld())).First();

            var netId = GetComponent<NetworkIdentity>();
            var board = BoardSpotsNet.Instance;

            if (netId != null && board != null)
            {
                if (_snapRoutine != null) { StopCoroutine(_snapRoutine); _snapRoutine = null; }

                board.RequestClaim(best.spotIndex, netId, (ok, center) =>
                {
                    if (ok)
                    {
                        Vector3 target = center;
                        if (keepCurrentZ) target.z = transform.position.z;

                        _occupiedSpot = best;
                        StartSnapTween(target, updateLastValid: true);

                        if (lockCoinAfterPlacement)
                        {
                            var guard = GetComponent<CoinPlacedLock>();
                            if (guard) guard.SetLocked(true);
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

            best.ForceOccupy(gameObject);
            Vector3 targetOffline = best.GetCenterWorld();
            if (keepCurrentZ) targetOffline.z = transform.position.z;
            _occupiedSpot = best;
            StartSnapTween(targetOffline, updateLastValid: true);
            return;
        }

        Vector3 fallback = _lastValidWorldPos;
        if (!keepCurrentZ) fallback.z = _spawnZ;
        StartSnapTween(fallback, updateLastValid: false);
    }

    void StartSnapTween(Vector3 target, bool updateLastValid)
    {
        if (_snapRoutine != null) StopCoroutine(_snapRoutine);
        _snapRoutine = StartCoroutine(SnapTweenRoutine(target, updateLastValid));
    }

    IEnumerator SnapTweenRoutine(Vector3 target, bool updateLastValid)
    {
        Vector3 start = transform.position;
        Vector3 originalScale = transform.localScale;

        if (snapDuration <= 0.0001f)
        {
            transform.position = target;
            if (_sync) _sync.OwnerSnapTo(target);
            if (updateLastValid) _lastValidWorldPos = target;
            yield break;
        }

        if (!landingPulse && tryFindPulseOnMainCamera && Camera.main)
            landingPulse = Camera.main.GetComponentInChildren<ImpactPulse>();

        float t = 0f;
        float lastY = start.y;

        while (t < snapDuration)
        {
            float p = t / snapDuration;
            float eased = snapEase != null ? snapEase.Evaluate(p) : p;

            Vector3 pos = Vector3.LerpUnclamped(start, target, eased);

            if (useHop && hopHeight > 0f)
            {
                float peak = Mathf.Clamp01(hopPeakT <= 0f ? 0.001f : (hopPeakT >= 1f ? 0.999f : hopPeakT));
                float h;
                if (p <= peak)
                {
                    float n = p / peak;
                    h = -((n - 1f) * (n - 1f)) + 1f;
                }
                else
                {
                    float n = (p - peak) / (1f - peak);
                    h = -(n * n) + 1f;
                }
                pos.y += hopHeight * Mathf.Max(0f, h);
            }

            transform.position = pos;

            float vy = (pos.y - lastY) / Mathf.Max(Time.deltaTime, 1e-5f);
            lastY = pos.y;

            if (useHop && vy < -0.001f)
            {
                float fallSpeed01 = 1f - Mathf.Exp(vy * fallStretchSensitivity * Time.deltaTime); // vy is negative
                float sx = Mathf.Lerp(1f, Mathf.Max(0.01f, fallSquashX), Mathf.Clamp01(fallSpeed01));
                float sy = Mathf.Lerp(1f, Mathf.Max(1f, fallStretchY), Mathf.Clamp01(fallSpeed01));
                transform.localScale = new Vector3(originalScale.x * sx, originalScale.y * sy, originalScale.z);
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, originalScale, 12f * Time.deltaTime);
            }

            if (sendNetworkDuringTween && _sync != null)
                _sync.OwnerSendPositionThrottled(pos);

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = target;
        transform.localScale = originalScale;
        if (_sync != null) _sync.OwnerSnapTo(target);
        if (updateLastValid) _lastValidWorldPos = target;
        if (landingPulse && updateLastValid) landingPulse.Play();
        _snapRoutine = null;

        yield return StartCoroutine(Co_LandingSquash(originalScale));
    }

    IEnumerator Co_LandingSquash(Vector3 originalScale)
    {
        if (landSquashTime <= 0f) yield break;

        Vector3 squash = new Vector3(originalScale.x * landSquashX, originalScale.y * landSquashY, originalScale.z);
        float t = 0f;
        float half = landSquashTime * 0.45f;
        float back = landSquashTime - half;

        while (t < half)
        {
            float e = landSquashEase.Evaluate(t / Mathf.Max(1e-6f, half));
            transform.localScale = Vector3.LerpUnclamped(originalScale, squash, e);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < back)
        {
            float e = landSquashEase.Evaluate(t / Mathf.Max(1e-6f, back));
            transform.localScale = Vector3.LerpUnclamped(squash, originalScale, e);
            t += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originalScale;
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
            var id = GetComponent<NetworkIdentity>();
            if (id && BoardSpotsNet.Instance)
            {
                BoardSpotsNet.Instance.CmdReleaseSpotByCoin(id.netId);
            }
            _occupiedSpot = null;
        }
        var lockGuard = GetComponent<CoinPlacedLock>();
        if (lockGuard != null) lockGuard.SetLocked(false);
    }

    void TryClaimAndSnap(ValidDropSpot spot)
    {
        var netId = GetComponent<NetworkIdentity>();
        var board = BoardSpotsNet.Instance;

        if (netId != null && board != null)
        {
            if (_snapRoutine != null) { StopCoroutine(_snapRoutine); _snapRoutine = null; }

            board.RequestClaim(spot.spotIndex, netId, (ok, center) =>
            {
                if (ok)
                {
                    Vector3 target = center;
                    if (keepCurrentZ) target.z = transform.position.z;

                    _occupiedSpot = spot;
                    StartSnapTween(target, updateLastValid: true);

                    if (lockCoinAfterPlacement)
                    {
                        var guard = GetComponent<CoinPlacedLock>();
                        if (guard) guard.SetLocked(true);
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

        spot.ForceOccupy(gameObject);
        Vector3 targetOffline = spot.GetCenterWorld();
        if (keepCurrentZ) targetOffline.z = transform.position.z;
        _occupiedSpot = spot;
        StartSnapTween(targetOffline, updateLastValid: true);
    }

    void RejectToLastValid()
    {
        Vector3 back = _lastValidWorldPos;
        if (!keepCurrentZ) back.z = _spawnZ;
        StartSnapTween(back, updateLastValid: false);
    }

}
