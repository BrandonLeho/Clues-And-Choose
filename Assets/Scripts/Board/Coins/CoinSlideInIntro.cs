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

    [Header("Intro Behaviour")]
    [SerializeField] bool useLocalOnlyIntro = true;
    [SerializeField] bool disableSyncDuringIntro = true;

    Vector3 _startPos;
    Vector3 _targetPos;
    bool _configured;

    [SyncVar] Vector3 syncStartPos;
    [SyncVar] Vector3 syncTargetPos;
    [SyncVar] double syncStartAtServerTime;
    [SyncVar] float syncSpeed;
    [SyncVar] float syncStartRotZ, syncEndRotZ;
    [SyncVar] float syncStartAlpha, syncEndAlpha;
    [SyncVar] float syncStartDelay;
    [SyncVar] bool syncUseLocalOnlyIntro;

    SpriteRenderer[] _srs;
    CoinDragSync _dragSync;

    void EnsureSRs()
    {
        if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void EnsureSyncComponents()
    {
        if (_dragSync == null) _dragSync = GetComponent<CoinDragSync>();
    }

    void SetSyncEnabled(bool v)
    {
        if (!disableSyncDuringIntro) return;
        EnsureSyncComponents();
        if (_dragSync) _dragSync.enabled = v;
    }

    public void Configure(Vector3 startPos, Vector3 targetPos, float delay,
                          float speed, float sRot, float eRot,
                          float sAlpha, float eAlpha, AnimationCurve curve,
                          bool localOnlyIntro = true)
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
        useLocalOnlyIntro = localOnlyIntro;
        _configured = true;

        syncStartPos = _startPos;
        syncTargetPos = _targetPos;
        syncSpeed = unitsPerSecond;
        syncStartRotZ = startRotZ;
        syncEndRotZ = endRotZ;
        syncStartAlpha = startAlpha;
        syncEndAlpha = endAlpha;
        syncStartDelay = startDelay;
        syncUseLocalOnlyIntro = useLocalOnlyIntro;
        syncStartAtServerTime = NetworkTime.time + startDelay;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureSRs();

        if (_configured)
        {
            transform.position = _targetPos;
            transform.rotation = Quaternion.Euler(0, 0, endRotZ);
            SetAlpha(endAlpha);

            var snap = GetComponent<CoinDropSnap>();
            if (snap) snap.SetHome(_targetPos, true);
            RpcSetHome(_targetPos);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (syncSpeed > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(Co_ClientLocalIntro(
                syncStartPos,
                syncTargetPos,
                syncStartDelay,
                syncSpeed,
                syncStartRotZ,
                syncEndRotZ,
                syncStartAlpha,
                syncEndAlpha,
                syncUseLocalOnlyIntro
            ));
        }
    }

    IEnumerator Co_ClientLocalIntro(Vector3 start, Vector3 target, float delay,
                                    float speed, float sRot, float eRot,
                                    float sAlpha, float eAlpha,
                                    bool localOnly)
    {
        EnsureSRs();

        SetSyncEnabled(false);

        transform.position = start;
        transform.rotation = Quaternion.Euler(0, 0, sRot);
        SetAlpha(sAlpha);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float dist = Vector3.Distance(start, target);
        float dur = Mathf.Max(0.0001f, dist / Mathf.Max(0.01f, speed));
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float u = Mathf.Clamp01(t);
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

        var snapFinal = GetComponent<CoinDropSnap>();
        if (snapFinal) snapFinal.SetHome(target, true);

        SetSyncEnabled(true);

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
        EnsureSRs();
        for (int i = 0; i < _srs.Length; i++)
        {
            var c = _srs[i].color;
            c.a = a;
            _srs[i].color = c;
        }
    }
}
