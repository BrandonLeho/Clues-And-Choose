using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class ImpactPulse : MonoBehaviour
{
    [Header("Targets")]
    public Camera targetCamera;
    public Volume targetVolume;
    public bool createTempVolumeIfMissing = true;

    [Header("Timing")]
    public bool useUnscaledTime = true;
    [Tooltip("Ease-in time to peak distortion/aberration/FOV")]
    public float riseTime = 0.08f;
    [Tooltip("Hold time at peak (optional)")]
    public float holdTime = 0.02f;
    [Tooltip("Ease-back time to baseline")]
    public float settleTime = 0.18f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("FOV Kick")]
    public bool doFovKick = true;
    [Tooltip("If <= 0, uses camera's current FOV as baseline each time you Play().")]
    public float baseFovOverride = 0f;
    public float fovKickAmount = 8f;

    [Header("Lens Distortion (URP)")]
    public bool doLensDistortion = true;
    [Tooltip("Negative = pincushion, Positive = barrel")]
    public float baseDistortion = 0f;
    public float peakDistortion = -0.55f;
    [Range(0.01f, 1.0f)] public float distortionScaleAtPeak = 1.0f;
    public Vector2 distortionCenter = new Vector2(0.5f, 0.5f);

    [Header("Chromatic Aberration (URP)")]
    public bool doChromatic = true;
    [Range(0f, 1f)] public float baseChromatic = 0f;
    [Range(0f, 1f)] public float peakChromatic = 0.65f;

    // Internals
    Volume _volume;
    VolumeProfile _profile;
    LensDistortion _lens;
    ChromaticAberration _ca;
    Coroutine _pulse;
    float _cachedBaseFov;

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
        EnsureVolumeAndOverrides();
    }

    void OnDisable()
    {
        if (_pulse != null) StopCoroutine(_pulse);
        RestoreBaselinesImmediate();
    }

    public void Play()
    {
        if (_pulse != null) StopCoroutine(_pulse);
        _pulse = StartCoroutine(Co_Pulse());
    }

    IEnumerator Co_Pulse()
    {
        EnsureVolumeAndOverrides();

        float baseFov = (baseFovOverride > 0f ? baseFovOverride : (targetCamera ? targetCamera.fieldOfView : 60f));
        _cachedBaseFov = baseFov;

        float startFov = baseFov;
        float endFov = baseFov + (doFovKick ? fovKickAmount : 0f);

        float startDist = baseDistortion;
        float endDist = doLensDistortion ? peakDistortion : baseDistortion;

        float startScale = 1f;
        float endScale = doLensDistortion ? distortionScaleAtPeak : 1f;

        float startCA = baseChromatic;
        float endCA = doChromatic ? peakChromatic : baseChromatic;

        float rt = Mathf.Max(1e-6f, riseTime);
        for (float t = 0f; t < rt; t += Delta())
        {
            float e = ease.Evaluate(t / rt);
            ApplyState(
                Mathf.Lerp(startFov, endFov, e),
                Mathf.Lerp(startDist, endDist, e),
                Vector2.LerpUnclamped(Vector2.one * startScale, Vector2.one * endScale, e).x,
                Vector2.LerpUnclamped(distortionCenter, distortionCenter, e), // keep center (but could animate)
                Mathf.Lerp(startCA, endCA, e)
            );
            yield return null;
        }
        ApplyState(endFov, endDist, endScale, distortionCenter, endCA);

        if (holdTime > 1e-6f) yield return Wait(holdTime);

        float st = Mathf.Max(1e-6f, settleTime);
        for (float t = 0f; t < st; t += Delta())
        {
            float e = ease.Evaluate(t / st);
            ApplyState(
                Mathf.Lerp(endFov, startFov, e),
                Mathf.Lerp(endDist, startDist, e),
                Mathf.Lerp(endScale, startScale, e),
                Vector2.LerpUnclamped(distortionCenter, distortionCenter, e),
                Mathf.Lerp(endCA, startCA, e)
            );
            yield return null;
        }
        ApplyState(startFov, startDist, startScale, distortionCenter, startCA);

        _pulse = null;
    }

    void ApplyState(float fov, float distortion, float scale, Vector2 center, float ca)
    {
        if (targetCamera && doFovKick)
            targetCamera.fieldOfView = fov;

        if (_lens && doLensDistortion)
        {
            _lens.active = true;
            _lens.intensity.Override(distortion);
            _lens.scale.Override(Mathf.Max(0.01f, scale));
            _lens.center.Override(center);
        }

        if (_ca && doChromatic)
        {
            _ca.active = true;
            _ca.intensity.Override(Mathf.Clamp01(ca));
        }
    }

    void RestoreBaselinesImmediate()
    {
        if (targetCamera && doFovKick)
            targetCamera.fieldOfView = (_cachedBaseFov > 0f ? _cachedBaseFov : targetCamera.fieldOfView);

        if (_lens)
        {
            _lens.intensity.Override(baseDistortion);
            _lens.scale.Override(1f);
            _lens.center.Override(distortionCenter);
        }
        if (_ca) _ca.intensity.Override(baseChromatic);
    }

    void EnsureVolumeAndOverrides()
    {
        if (!targetVolume && createTempVolumeIfMissing)
        {
            targetVolume = FindFirstObjectByType<Volume>();
            if (!targetVolume)
            {
                var go = new GameObject("ImpactPulse_TempVolume");
                go.hideFlags = HideFlags.DontSave;
                targetVolume = go.AddComponent<Volume>();
                targetVolume.isGlobal = true;
                targetVolume.priority = 999f;
            }
        }

        _volume = targetVolume;
        if (_volume)
        {
            if (_volume.profile == null)
                _volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile = _volume.profile;

            if (!_profile.TryGet(out _lens))
            {
                _lens = _profile.Add<LensDistortion>(true);
                _lens.intensity.overrideState = true;
                _lens.scale.overrideState = true;
                _lens.center.overrideState = true;
            }
            if (!_profile.TryGet(out _ca))
            {
                _ca = _profile.Add<ChromaticAberration>(true);
                _ca.intensity.overrideState = true;
            }
        }
    }

    float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    WaitForSecondsRealtime Wait(float s) => useUnscaledTime ? new WaitForSecondsRealtime(s) : null;
}
