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
    public float riseTime = 0.08f;
    public float holdTime = 0.02f;
    public float settleTime = 0.18f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Zoom Effect")]
    public bool doZoom = true;
    public float baseZoomOverride = 0f;
    public float zoomAmount = 0.8f;

    [Header("Lens Distortion")]
    public bool doLensDistortion = true;
    public float baseDistortion = 0f;
    public float peakDistortion = -0.55f;
    [Range(0.01f, 1.0f)] public float distortionScaleAtPeak = 1.0f;
    public Vector2 distortionCenter = new Vector2(0.5f, 0.5f);

    [Header("Chromatic Aberration")]
    public bool doChromatic = true;
    [Range(0f, 1f)] public float baseChromatic = 0f;
    [Range(0f, 1f)] public float peakChromatic = 0.65f;

    Volume _volume;
    VolumeProfile _profile;
    LensDistortion _lens;
    ChromaticAberration _ca;
    Coroutine _pulse;

    float _cachedBaseOrthoSize;
    Vector3 _cachedBaseScale;

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

        if (targetCamera)
        {
            if (targetCamera.orthographic)
                _cachedBaseOrthoSize = (baseZoomOverride > 0f ? baseZoomOverride : targetCamera.orthographicSize);
            else
                _cachedBaseScale = targetCamera.transform.localScale;
        }

        float startSize = targetCamera && targetCamera.orthographic ? _cachedBaseOrthoSize : 1f;
        float endSize = targetCamera && targetCamera.orthographic ? startSize * zoomAmount : 1f;

        Vector3 startScale = _cachedBaseScale == Vector3.zero ? Vector3.one : _cachedBaseScale;
        Vector3 endScale = startScale * zoomAmount;

        float startDist = baseDistortion;
        float endDist = doLensDistortion ? peakDistortion : baseDistortion;

        float startScaleLD = 1f;
        float endScaleLD = doLensDistortion ? distortionScaleAtPeak : 1f;

        float startCA = baseChromatic;
        float endCA = doChromatic ? peakChromatic : baseChromatic;

        float rt = Mathf.Max(1e-6f, riseTime);
        for (float t = 0f; t < rt; t += Delta())
        {
            float e = ease.Evaluate(t / rt);
            ApplyState(
                Mathf.Lerp(startSize, endSize, e),
                Vector3.Lerp(startScale, endScale, e),
                Mathf.Lerp(startDist, endDist, e),
                Mathf.Lerp(startScaleLD, endScaleLD, e),
                Mathf.Lerp(startCA, endCA, e)
            );
            yield return null;
        }
        ApplyState(endSize, endScale, endDist, endScaleLD, endCA);

        if (holdTime > 1e-6f) yield return Wait(holdTime);

        float st = Mathf.Max(1e-6f, settleTime);
        for (float t = 0f; t < st; t += Delta())
        {
            float e = ease.Evaluate(t / st);
            ApplyState(
                Mathf.Lerp(endSize, startSize, e),
                Vector3.Lerp(endScale, startScale, e),
                Mathf.Lerp(endDist, startDist, e),
                Mathf.Lerp(endScaleLD, startScaleLD, e),
                Mathf.Lerp(endCA, startCA, e)
            );
            yield return null;
        }
        ApplyState(startSize, startScale, startDist, startScaleLD, startCA);

        _pulse = null;
    }

    void ApplyState(float orthoSize, Vector3 scale, float distortion, float scaleLD, float ca)
    {
        if (targetCamera && doZoom)
        {
            if (targetCamera.orthographic)
                targetCamera.orthographicSize = orthoSize;
            else
                targetCamera.transform.localScale = scale;
        }

        if (_lens && doLensDistortion)
        {
            _lens.active = true;
            _lens.intensity.Override(distortion);
            _lens.scale.Override(Mathf.Max(0.01f, scaleLD));
            _lens.center.Override(distortionCenter);
        }

        if (_ca && doChromatic)
        {
            _ca.active = true;
            _ca.intensity.Override(Mathf.Clamp01(ca));
        }
    }

    void RestoreBaselinesImmediate()
    {
        if (targetCamera && doZoom)
        {
            if (targetCamera.orthographic)
                targetCamera.orthographicSize = _cachedBaseOrthoSize > 0f ? _cachedBaseOrthoSize : targetCamera.orthographicSize;
            else if (_cachedBaseScale != Vector3.zero)
                targetCamera.transform.localScale = _cachedBaseScale;
        }

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
