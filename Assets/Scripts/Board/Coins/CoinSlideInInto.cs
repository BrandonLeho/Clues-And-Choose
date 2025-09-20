using System.Collections;
using UnityEngine;
using Mirror;

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

    SpriteRenderer[] _srs;

    public void Configure(Vector3 startPos, Vector3 targetPos, float delay,
                          float speed, float startR, float endR,
                          float sAlpha, float eAlpha, AnimationCurve curve)
    {
        _startPos = startPos;
        _targetPos = targetPos;
        startDelay = delay;
        unitsPerSecond = Mathf.Max(0.01f, speed);
        startRotZ = startR;
        endRotZ = endR;
        startAlpha = Mathf.Clamp01(sAlpha);
        endAlpha = Mathf.Clamp01(eAlpha);
        ease = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        _configured = true;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (_configured) StartCoroutine(Co_RunServer());
    }

    [Server]
    IEnumerator Co_RunServer()
    {
        transform.position = _startPos;
        transform.rotation = Quaternion.Euler(0, 0, startRotZ);
        SetAlpha(startAlpha);

        double serverTime = NetworkTime.time;
        RpcStartSlide(_startPos, _targetPos, serverTime, startDelay,
                    unitsPerSecond, startRotZ, endRotZ, startAlpha, endAlpha);

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float dist = Vector3.Distance(_startPos, _targetPos);
        float dur = Mathf.Max(0.0001f, dist / unitsPerSecond);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float u = ease.Evaluate(Mathf.Clamp01(t));
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


    void SetAlpha(float a)
    {
        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < _srs.Length; i++)
        {
            var c = _srs[i].color; c.a = a; _srs[i].color = c;
        }
    }

    [ClientRpc]
    void RpcSetHome(Vector3 finalPos)
    {
        var snap = GetComponent<CoinDropSnap>();
        if (snap) snap.SetHome(finalPos, true);
    }

    [ClientRpc]
    void RpcStartSlide(Vector3 start, Vector3 target, double serverTime, float delay,
                    float speed, float sRot, float eRot, float sAlpha, float eAlpha)
    {
        if (isServer) return;

        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);
        StopAllCoroutines();
        StartCoroutine(Co_RunClient(start, target, serverTime, delay, speed,
                                    sRot, eRot, sAlpha, eAlpha));
    }

    IEnumerator Co_RunClient(Vector3 start, Vector3 target, double serverTime, float delay,
                            float speed, float sRot, float eRot, float sAlpha, float eAlpha)
    {
        transform.position = start;
        transform.rotation = Quaternion.Euler(0, 0, sRot);
        SetAlpha(sAlpha);

        double startAt = serverTime + delay;
        while (NetworkTime.time < startAt) yield return null;

        float dist = Vector3.Distance(start, target);
        float dur = Mathf.Max(0.0001f, dist / Mathf.Max(0.01f, speed));
        double endAt = startAt + dur;

        while (NetworkTime.time < endAt)
        {
            float u = Mathf.Clamp01((float)((NetworkTime.time - startAt) / dur));
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

}
