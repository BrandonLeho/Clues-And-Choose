using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class CoinLockIconAnimator : MonoBehaviour
{
    [Header("Scale")]
    [Min(0f)] public float baseScale = 1f;
    [Min(0f)] public float upscaledMultiplier = 1.2f;

    [Header("Alpha")]
    [Range(0f, 1f)] public float baseAlpha = 1f;

    [Header("Timing (seconds)")]
    [Min(0.01f)] public float fadeInDuration = 0.20f;
    [Min(0.01f)] public float fadeOutDuration = 0.20f;
    [Min(0.01f)] public float scaleInDuration = 0.20f;
    [Min(0.01f)] public float scaleOutDuration = 0.20f;

    [Header("Ease")]
    [Range(0f, 1f)] public float easeInBias = 0.6f;
    [Range(0f, 1f)] public float easeOutBias = 0.6f;

    SpriteRenderer _sr;
    Coroutine _anim;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        SetAlpha(0f);
        SetScale(baseScale * upscaledMultiplier);
    }

    void OnEnable()
    {
        CoinRoundLockManager.OnLocked += HandleLocked;
        CoinRoundLockManager.OnUnlocked += HandleUnlocked;
    }

    void OnDisable()
    {
        CoinRoundLockManager.OnLocked -= HandleLocked;
        CoinRoundLockManager.OnUnlocked -= HandleUnlocked;
    }

    void HandleLocked()
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateIn());
    }

    void HandleUnlocked()
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateOut());
    }

    IEnumerator AnimateIn()
    {
        float a0 = _sr.color.a, a1 = Mathf.Clamp01(baseAlpha);
        float tA = 0f;
        float s0 = transform.localScale.x, s1 = baseScale;
        float tS = 0f;

        while (tA < fadeInDuration || tS < scaleInDuration)
        {
            if (tA < fadeInDuration)
            {
                tA += Time.deltaTime;
                float uA = Mathf.Clamp01(tA / fadeInDuration);
                uA = Smooth(uA, easeInBias);
                SetAlpha(Mathf.Lerp(a0, a1, uA));
            }

            if (tS < scaleInDuration)
            {
                tS += Time.deltaTime;
                float uS = Mathf.Clamp01(tS / scaleInDuration);
                uS = Smooth(uS, easeInBias);
                SetScale(Mathf.Lerp(s0, s1, uS));
            }

            yield return null;
        }
        _anim = null;
    }

    IEnumerator AnimateOut()
    {
        float a0 = _sr.color.a, a1 = 0f;
        float tA = 0f;
        float s0 = transform.localScale.x, s1 = baseScale * upscaledMultiplier;
        float tS = 0f;

        while (tA < fadeOutDuration || tS < scaleOutDuration)
        {
            if (tA < fadeOutDuration)
            {
                tA += Time.deltaTime;
                float uA = Mathf.Clamp01(tA / fadeOutDuration);
                uA = Smooth(uA, easeOutBias);
                SetAlpha(Mathf.Lerp(a0, a1, uA));
            }

            if (tS < scaleOutDuration)
            {
                tS += Time.deltaTime;
                float uS = Mathf.Clamp01(tS / scaleOutDuration);
                uS = Smooth(uS, easeOutBias);
                SetScale(Mathf.Lerp(s0, s1, uS));
            }

            yield return null;
        }
        _anim = null;
    }

    float Smooth(float u, float bias)
    {
        if (bias <= 0f) return u;
        float s = Mathf.SmoothStep(0f, 1f, u);
        return Mathf.Lerp(u, s, bias);
    }

    void SetAlpha(float a)
    {
        a = Mathf.Clamp01(a);
        var c = _sr.color;
        c.a = a;
        _sr.color = c;
    }

    void SetScale(float s)
    {
        transform.localScale = new Vector3(s, s, 1f);
    }

#if UNITY_EDITOR
    [ContextMenu("Preview: Locked (In)")]
    void _PreviewIn() => HandleLocked();

    [ContextMenu("Preview: Unlocked (Out)")]
    void _PreviewOut() => HandleUnlocked();
#endif
}
