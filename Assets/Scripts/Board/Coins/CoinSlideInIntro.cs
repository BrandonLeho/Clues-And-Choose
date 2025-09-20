using System.Collections;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class CoinSlideInIntro : NetworkBehaviour
{
    [Header("Motion")]
    [Min(0.01f)] public float unitsPerSecond = 6f;
    [Min(0f)] public float startDelay = 0f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Appearance")]
    public float startRotZ = 0f;
    public float endRotZ = 0f;
    [Range(0, 1)] public float startAlpha = 1f;
    [Range(0, 1)] public float endAlpha = 1f;

    Vector3 _startPos;
    Vector3 _targetPos;
    bool _configured;

    [SyncVar] Vector3 syncStartPos;
    [SyncVar] Vector3 syncTargetPos;
    [SyncVar] double syncStartAtServerTime;
    [SyncVar] float syncSpeed;
    [SyncVar] float syncStartRotZ, syncEndRotZ;
    [SyncVar] float syncStartAlpha, syncEndAlpha;

    SpriteRenderer[] _srs;
    bool _clientAnimStarted;

    public void Configure(Vector3 startPos, Vector3 targetPos, float delay,
                          float speed, float sRot, float eRot,
                          float sAlpha, float eAlpha, AnimationCurve curve)
    {
        _startPos = startPos;
        _targetPos = targetPos;
        startDelay = delay;
        unitsPerSecond = Mathf.Max(0.01f, speed);
        startRotZ = sRot;
        endRotZ = eRot;
        startAlpha = Mathf.Clamp01(sAlpha);
        endAlpha = Mathf.Clamp01(eAlpha);
        ease = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        _configured = true;

        syncStartPos = _startPos;
        syncTargetPos = _targetPos;
        syncSpeed = unitsPerSecond;
        syncStartRotZ = startRotZ;
        syncEndRotZ = endRotZ;
        syncStartAlpha = startAlpha;
        syncEndAlpha = endAlpha;
        syncStartAtServerTime = NetworkTime.time + startDelay;
    }

    void EnsureSRs()
    {
        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureSRs();
        if (_configured) StartCoroutine(Co_ServerAnim());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (isServer) return;

        if (_clientAnimStarted) return;

        if (syncSpeed > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(Co_ClientAnim(syncStartPos, syncTargetPos, syncStartAtServerTime,
                                         syncSpeed, syncStartRotZ, syncEndRotZ,
                                         syncStartAlpha, syncEndAlpha));
            _clientAnimStarted = true;
        }
    }

    [Server]
    IEnumerator Co_ServerAnim()
    {
        transform.position = _startPos;
        transform.rotation = Quaternion.Euler(0, 0, startRotZ);
        SetAlpha(startAlpha);

        while (NetworkTime.time < syncStartAtServerTime) yield return null;

        yield return null;

        RpcStartSlide(syncStartPos, syncTargetPos, syncStartAtServerTime,
                      syncSpeed, syncStartRotZ, syncEndRotZ,
                      syncStartAlpha, syncEndAlpha);

        float dist = Vector3.Distance(_startPos, _targetPos);
        float dur = Mathf.Max(0.0001f, dist / unitsPerSecond);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float u = ease != null ? ease.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.LerpUnclamped(_startPos, _targetPos, u);
            float r = Mathf.LerpAngle(startRotZ, endRotZ, u);
            transform.rotation = Quaternion.Euler(0, 0, r);
            SetAlpha(Mathf.Lerp(startAlpha, endAlpha, u));
            yield return null;
        }

        transform.position = _targetPos;
        transform.rotation = Quaternion.Euler(0, 0, endRotZ);
        SetAlpha(endAlpha);

        var snap = GetComponent<CoinDropSnap>();
        if (snap) snap.SetHome(_targetPos, true);
        RpcSetHome(_targetPos);

        enabled = false;
    }

    [ClientRpc]
    void RpcStartSlide(Vector3 start, Vector3 target, double startAtServerTime,
                       float speed, float sRot, float eRot, float sAlpha, float eAlpha)
    {
        if (isServer) return;
        if (_clientAnimStarted) return;

        EnsureSRs();
        StopAllCoroutines();
        StartCoroutine(Co_ClientAnim(start, target, startAtServerTime, speed, sRot, eRot, sAlpha, eAlpha));
        _clientAnimStarted = true;
    }

    IEnumerator Co_ClientAnim(Vector3 start, Vector3 target, double startAtServerTime,
                              float speed, float sRot, float eRot, float sAlpha, float eAlpha)
    {
        // Place at start pose
        transform.position = start;
        transform.rotation = Quaternion.Euler(0, 0, sRot);
        SetAlpha(sAlpha);

        // Wait for the synchronized start time
        while (NetworkTime.time < startAtServerTime) yield return null;

        float dist = Vector3.Distance(start, target);
        float dur = Mathf.Max(0.0001f, dist / Mathf.Max(0.01f, speed));
        double endAt = startAtServerTime + dur;

        while (NetworkTime.time < endAt)
        {
            float u = Mathf.Clamp01((float)((NetworkTime.time - startAtServerTime) / dur));
            float e = ease != null ? ease.Evaluate(u) : Mathf.SmoothStep(0f, 1f, u);
            transform.position = Vector3.LerpUnclamped(start, target, e);
            float r = Mathf.LerpAngle(sRot, eRot, e);
            transform.rotation = Quaternion.Euler(0, 0, r);
            SetAlpha(Mathf.Lerp(sAlpha, eAlpha, e));
            yield return null;
        }

        transform.position = target;
        transform.rotation = Quaternion.Euler(0, 0, eRot);
        SetAlpha(eAlpha);
    }

    [ClientRpc]
    void RpcSetHome(Vector3 finalPos)
    {
        var snap = GetComponent<CoinDropSnap>();
        if (snap) snap.SetHome(finalPos, true);
    }

    void SetAlpha(float a)
    {
        EnsureSRs();
        for (int i = 0; i < _srs.Length; i++)
        {
            var c = _srs[i].color; c.a = a; _srs[i].color = c;
        }
    }
}
