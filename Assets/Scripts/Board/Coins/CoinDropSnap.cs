using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Final Shrink")]
    [Range(0f, 1.0f)] public float finalShrink = 0.96f;

    [Header("Landing Pulse")]
    public ImpactPulse landingPulse;
    public bool tryFindPulseOnMainCamera = true;

    [Header("Ring Ripple")]
    public RectTransform ringPrefab;
    [Min(0)] public int ringCount = 2;
    public float ringInterval = 0.05f;
    public float ringDuration = 0.18f;
    public AnimationCurve ringEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float ringStartScale = 0.6f;
    public float ringEndScale = 1.4f;
    public float ringStartAlpha = 1f;

    [Header("Ring Mask Target (auto-assigned)")]
    public string colorGridName = "ColorGrid";
    public bool autoAddRectMask2DIfMissing = false;

    [Header("Contact Detection")]
    public float landingTriggerDistance = 0.01f;

    Vector3 _lastValidWorldPos;
    float _spawnZ;
    CoinDragHandler _drag;
    CoinDragSync _sync;
    Coroutine _snapRoutine;
    ValidDropSpot _occupiedSpot;
    CoinPlacementProbe _probe;

    RectTransform _colorGridRT;
    bool _firedContactFX;

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        _sync = GetComponent<CoinDragSync>();
        _probe = GetComponent<CoinPlacementProbe>();

        _drag.onPickUp.AddListener(OnPickUp);
        _drag.onDrop.AddListener(OnDrop);

        EnsureColorGridRef();
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

    void EnsureColorGridRef()
    {
        if (_colorGridRT) return;
        var go = GameObject.Find(colorGridName);
        if (go) _colorGridRT = go.transform as RectTransform;
        if (_colorGridRT && autoAddRectMask2DIfMissing)
        {
            if (!_colorGridRT.GetComponent<RectMask2D>() && !_colorGridRT.GetComponent<Mask>())
                _colorGridRT.gameObject.AddComponent<RectMask2D>();
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
                if (forcedSpot.enabledForPlacement) { TryClaimAndSnap(forcedSpot); }
                else { RejectToLastValid(); }
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
                        Vector3 target = best != null ? best.GetCenterWorld() : center;
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
        _firedContactFX = false;

        if (snapDuration <= 0.0001f)
        {
            transform.position = target;
            if (_sync) _sync.OwnerSnapTo(target, transform.localScale);
            if (updateLastValid) _lastValidWorldPos = target;

            if (updateLastValid)
            {
                yield return StartCoroutine(Co_LandingSquash(originalScale, true));
            }
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
                if (p <= peak) { float n = p / peak; h = -((n - 1f) * (n - 1f)) + 1f; }
                else { float n = (p - peak) / (1f - peak); h = -(n * n) + 1f; }
                pos.y += hopHeight * Mathf.Max(0f, h);
            }

            transform.position = pos;

            float vy = (pos.y - lastY) / Mathf.Max(Time.deltaTime, 1e-5f);
            lastY = pos.y;

            if (useHop && vy < -0.001f)
            {
                float fallSpeed01 = 1f - Mathf.Exp(vy * fallStretchSensitivity * Time.deltaTime);
                float sx = Mathf.Lerp(1f, Mathf.Max(0.01f, fallSquashX), Mathf.Clamp01(fallSpeed01));
                float sy = Mathf.Lerp(1f, Mathf.Max(1f, fallStretchY), Mathf.Clamp01(fallSpeed01));
                transform.localScale = new Vector3(originalScale.x * sx, originalScale.y * sy, originalScale.z);
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, originalScale, 12f * Time.deltaTime);
            }

            bool nearEnd = (snapDuration - t) <= 0.03f;
            if (sendNetworkDuringTween && _sync != null && !nearEnd)
                _sync.OwnerSendPositionThrottled(pos, transform.localScale);


            t += Time.deltaTime;
            yield return null;
        }

        transform.position = target;
        transform.localScale = originalScale;
        if (_sync != null) _sync.OwnerSnapTo(target, transform.localScale);
        if (updateLastValid) _lastValidWorldPos = target;

        if (updateLastValid && !_firedContactFX)
            FireContactFX(target);

        _snapRoutine = null;

        yield return StartCoroutine(Co_LandingSquash(originalScale, updateLastValid));
    }

    void FireContactFX(Vector3 worldPos)
    {
        _firedContactFX = true;
        if (landingPulse) landingPulse.Play();
        if (ringCount > 0 && ringPrefab && _colorGridRT)
            StartCoroutine(Co_RingBurst(worldPos));
    }

    IEnumerator Co_LandingSquash(Vector3 originalScale, bool doFx)
    {
        if (doFx) FireContactFX(transform.position);

        if (landSquashTime <= 0f)
        {
            transform.localScale = originalScale * finalShrink;
            if (sendNetworkDuringTween && _sync != null)
                _sync.OwnerSendPositionThrottled(transform.position, transform.localScale);
            yield break;
        }

        Vector3 squash = new Vector3(originalScale.x * landSquashX, originalScale.y * landSquashY, originalScale.z);
        Vector3 finalScale = originalScale * finalShrink;

        float t = 0f;
        float half = landSquashTime * 0.45f;
        float back = landSquashTime - half;

        while (t < half)
        {
            float e = landSquashEase.Evaluate(t / Mathf.Max(1e-6f, half));
            transform.localScale = Vector3.LerpUnclamped(originalScale, squash, e);

            if (sendNetworkDuringTween && _sync != null)
                _sync.OwnerSendPositionThrottled(transform.position, transform.localScale);

            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < back)
        {
            float e = landSquashEase.Evaluate(t / Mathf.Max(1e-6f, back));
            transform.localScale = Vector3.LerpUnclamped(squash, finalScale, e);

            if (sendNetworkDuringTween && _sync != null)
                _sync.OwnerSendPositionThrottled(transform.position, transform.localScale);

            t += Time.deltaTime;
            yield return null;
        }

        transform.localScale = finalScale;

        if (sendNetworkDuringTween && _sync != null)
            _sync.OwnerSendPositionThrottled(transform.position, transform.localScale);
    }

    IEnumerator Co_RingBurst(Vector3 worldPos)
    {
        EnsureColorGridRef();
        if (!_colorGridRT) yield break;

        var canvas = _colorGridRT.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos);
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_colorGridRT, screen, uiCam, out local))
            yield break;

        for (int i = 0; i < ringCount; i++)
        {
            var ring = Instantiate(ringPrefab, _colorGridRT);
            var rt = ring as RectTransform;
            rt.anchoredPosition = local;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * ringStartScale;

            var cg = ring.GetComponent<CanvasGroup>() ?? ring.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = ringStartAlpha;

            StartCoroutine(Co_RingOne(rt, cg));

            if (i < ringCount - 1 && ringInterval > 0f)
                yield return new WaitForSeconds(ringInterval);
        }
    }

    IEnumerator Co_RingOne(RectTransform rt, CanvasGroup cg)
    {
        float t = 0f;
        float d = Mathf.Max(0.0001f, ringDuration);
        while (t < d)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / d);
            float e = ringEase.Evaluate(p);

            float s = Mathf.LerpUnclamped(ringStartScale, ringEndScale, e);
            rt.localScale = new Vector3(s, s, 1f);
            cg.alpha = 1f - p;

            yield return null;
        }
        if (rt) Destroy(rt.gameObject);
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
                    Vector3 target = spot != null ? spot.GetCenterWorld() : center;
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
