using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PerimeterGlowEntry : MonoBehaviour
{
    [Header("Targets")]
    public PerimeterGlowGraphic glow;
    public ColorGridAnimator gridAnimator;

    [Header("Behavior")]
    public bool disableGlowUntilEntry = true;
    public bool playWhenGridFinishes = true;
    public float startDelay = 0.05f;
    public bool useUnscaledTime = true;

    [Header("Entry")]
    public float targetIntensity = 0f;
    public float flashMultiplier = 2.2f;
    public float riseTime = 0.30f;
    public float settleTime = 0.65f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional Pulse")]
    public bool alsoPulseWaves = true;
    public float flashWavesAdd = 0.20f;

    [Header("Idle Handoff")]
    public float idleBlendTime = 0.35f;
    public AnimationCurve idleEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float idleIntensity = -1f;
    public float idleWaves = -1f;
    public float idleWaveSpeed = -1000f;
    public float idleOverallPulse = -1f;

    Material _mat;
    Coroutine _running;

    void Reset() => glow = GetComponent<PerimeterGlowGraphic>();

    void OnEnable()
    {
        if (!glow) glow = GetComponent<PerimeterGlowGraphic>();
        if (gridAnimator && playWhenGridFinishes)
        {
            gridAnimator.OnAnimationComplete.RemoveListener(OnGridDone);
            gridAnimator.OnAnimationComplete.AddListener(OnGridDone);
        }
        if (disableGlowUntilEntry && glow) glow.enabled = false;
        CacheMat();
        Set("_Intensity", 0f);
    }

    void OnDisable()
    {
        if (gridAnimator) gridAnimator.OnAnimationComplete.RemoveListener(OnGridDone);
    }

    void OnGridDone() => Play();

    public void Play()
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Co_Entry());
    }

    IEnumerator Co_Entry()
    {
        if (glow && !glow.enabled) glow.enabled = true;
        CacheMat();
        if (_mat == null) yield break;

        float baseTarget = targetIntensity > 0f ? targetIntensity : (glow ? glow.intensity : 1.2f);
        float flash = baseTarget * Mathf.Max(1f, flashMultiplier);

        float startWaves = Get("_Waves", 0f);
        float startWaveSpeed = Get("_WaveSpeed", 1.5f);
        float startOverall = Get("_OverallPulse", 0f);

        Set("_Intensity", 0f);
        if (startDelay > 0f) yield return Wait(startDelay);

        for (float t = 0f; t < Mathf.Max(1e-6f, riseTime); t += Delta())
        {
            float e = ease.Evaluate(t / Mathf.Max(1e-6f, riseTime));
            Set("_Intensity", Mathf.LerpUnclamped(0f, flash, e));
            if (alsoPulseWaves) Set("_Waves", Mathf.LerpUnclamped(startWaves, startWaves + flashWavesAdd, e));
            yield return null;
        }

        for (float t = 0f; t < Mathf.Max(1e-6f, settleTime); t += Delta())
        {
            float e = ease.Evaluate(t / Mathf.Max(1e-6f, settleTime));
            Set("_Intensity", Mathf.LerpUnclamped(flash, baseTarget, e));
            if (alsoPulseWaves) Set("_Waves", Mathf.LerpUnclamped(startWaves + flashWavesAdd, startWaves, e));
            yield return null;
        }

        float entryIntensity = baseTarget;
        float entryWaves = Get("_Waves", startWaves);
        float entryWaveSpeed = Get("_WaveSpeed", startWaveSpeed);
        float entryOverall = Get("_OverallPulse", startOverall);

        float tgtIntensity = idleIntensity > 0f ? idleIntensity : entryIntensity;
        float tgtWaves = idleWaves >= 0f ? idleWaves : entryWaves;
        float tgtWaveSpeed = idleWaveSpeed > -999f ? idleWaveSpeed : entryWaveSpeed;
        float tgtOverall = idleOverallPulse >= 0f ? idleOverallPulse : entryOverall;

        if (idleBlendTime > 1e-4f)
        {
            for (float t = 0f; t < idleBlendTime; t += Delta())
            {
                float e = idleEase.Evaluate(t / idleBlendTime);
                Set("_Intensity", Mathf.LerpUnclamped(entryIntensity, tgtIntensity, e));
                Set("_Waves", Mathf.LerpUnclamped(entryWaves, tgtWaves, e));
                Set("_WaveSpeed", Mathf.LerpUnclamped(entryWaveSpeed, tgtWaveSpeed, e));
                Set("_OverallPulse", Mathf.LerpUnclamped(entryOverall, tgtOverall, e));
                yield return null;
            }
        }

        Set("_Intensity", tgtIntensity);
        Set("_Waves", tgtWaves);
        Set("_WaveSpeed", tgtWaveSpeed);
        Set("_OverallPulse", tgtOverall);

        _running = null;
    }

    void CacheMat() { if (glow) _mat = glow.materialForRendering; }
    float Get(string p, float fallback) => _mat && _mat.HasFloat(p) ? _mat.GetFloat(p) : fallback;
    void Set(string p, float v) { if (_mat && _mat.HasFloat(p)) _mat.SetFloat(p, v); }
    float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    WaitForSecondsRealtime Wait(float s) => useUnscaledTime ? new WaitForSecondsRealtime(s) : null;
}
