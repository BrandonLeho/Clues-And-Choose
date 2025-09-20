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
    bool _playedServer;

    SpriteRenderer[] _srs;

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
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_configured && !_playedServer)
        {
            _playedServer = true;

            StartCoroutine(Co_RunServer());

            RpcStartIntro(_startPos, _targetPos, startDelay, unitsPerSecond,
                          startRotZ, endRotZ, startAlpha, endAlpha);
        }
    }

    [ClientRpc]
    void RpcStartIntro(Vector3 startPos, Vector3 targetPos, float delay, float speed,
                       float sRot, float eRot, float sAlpha, float eAlpha)
    {
        if (isServer) return;

        _startPos = startPos;
        _targetPos = targetPos;
        startDelay = delay;
        unitsPerSecond = speed;
        startRotZ = sRot;
        endRotZ = eRot;
        startAlpha = sAlpha;
        endAlpha = eAlpha;
        _configured = true;

        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);
        StartCoroutine(Co_RunClient());
    }

    IEnumerator Co_RunServer()
    {
        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);

        transform.position = _startPos;
        transform.rotation = Quaternion.Euler(0, 0, startRotZ);
        SetAlpha(startAlpha);

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float distance = Vector3.Distance(_startPos, _targetPos);
        float dur = Mathf.Max(0.0001f, distance / unitsPerSecond);

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

    IEnumerator Co_RunClient()
    {
        transform.position = _startPos;
        transform.rotation = Quaternion.Euler(0, 0, startRotZ);
        SetAlpha(startAlpha);

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float distance = Vector3.Distance(_startPos, _targetPos);
        float dur = Mathf.Max(0.0001f, distance / unitsPerSecond);

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

        enabled = false;
    }

    [ClientRpc]
    void RpcSetHome(Vector3 finalPos)
    {
        var snap = GetComponent<CoinDropSnap>();
        if (snap) snap.SetHome(finalPos, true);
    }

    void SetAlpha(float a)
    {
        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < _srs.Length; i++)
        {
            var c = _srs[i].color;
            c.a = a;
            _srs[i].color = c;
        }
    }
}
