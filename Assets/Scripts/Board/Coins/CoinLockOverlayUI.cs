using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoinLockOverlayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Image backgroundImage;
    [SerializeField] Image lockImage;
    [SerializeField] RectTransform lockRect;

    [Header("Timings")]
    [SerializeField] float bgFadeDuration = 0.25f;
    [SerializeField] float lockInDuration = 0.35f;
    [SerializeField] float lockOutDuration = 0.25f;

    [Header("Alpha")]
    [SerializeField, Range(0f, 1f)] float bgLockedAlpha = 0.55f;
    [SerializeField, Range(0f, 1f)] float bgUnlockedAlpha = 0f;
    [SerializeField, Range(0f, 1f)] float lockLockedAlpha = 1f;
    [SerializeField, Range(0f, 1f)] float lockUnlockedAlpha = 0f;

    [Header("Scale")]
    [SerializeField] float lockedScale = 1.0f;
    [SerializeField] float lockEnterOverscale = 1.6f;
    [SerializeField] float unlockExitOverscale = 1.6f;

    [Header("Easing")]
    [SerializeField] AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] AnimationCurve scaleInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] AnimationCurve scaleOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Behaviour")]
    [SerializeField] bool readInitialFromManager = true;
    [SerializeField] bool startLockedFallback = true;
    [SerializeField] bool backgroundBlocksRaycastsWhenLocked = false;

    Coroutine _anim;

    void Reset()
    {
        if (!backgroundImage) backgroundImage = GetComponent<Image>();
        if (!lockRect) lockRect = transform.GetComponentInChildren<RectTransform>(true);
        if (!lockImage && lockRect) lockImage = lockRect.GetComponent<Image>();
    }

    void Awake()
    {
        if (!backgroundImage) backgroundImage = GetComponent<Image>();
        if (!lockImage && lockRect) lockImage = lockRect.GetComponent<Image>();
        if (!lockRect && lockImage) lockRect = lockImage.rectTransform;
    }

    void OnEnable()
    {
        CoinRoundLockManager.onGlobalLockStateChanged += OnGlobalLockStateChanged;

        bool startLocked = readInitialFromManager
            ? CoinRoundLockManager.IsLockedGlobally
            : startLockedFallback;

        SetInstant(startLocked);
    }

    void OnDisable()
    {
        CoinRoundLockManager.onGlobalLockStateChanged -= OnGlobalLockStateChanged;
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }
    }

    void OnGlobalLockStateChanged(bool locked)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(locked ? PlayLock() : PlayUnlock());
    }

    public void SetInstant(bool locked)
    {
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }

        SetImageAlpha(backgroundImage, locked ? bgLockedAlpha : bgUnlockedAlpha);
        if (lockRect) lockRect.localScale = Vector3.one * (locked ? lockedScale : unlockExitOverscale);
        SetImageAlpha(lockImage, locked ? lockLockedAlpha : lockUnlockedAlpha);

        if (backgroundImage) backgroundImage.raycastTarget = locked && backgroundBlocksRaycastsWhenLocked;
        if (lockImage) lockImage.raycastTarget = locked && backgroundBlocksRaycastsWhenLocked;
    }

    IEnumerator PlayLock()
    {
        float tBg = 0f;
        float startBg = GetAlpha(backgroundImage);
        float endBg = bgLockedAlpha;

        float tLock = 0f;
        float startScale = lockEnterOverscale;
        float endScale = lockedScale;
        float startLockA = GetAlpha(lockImage);
        float endLockA = lockLockedAlpha;

        if (lockRect) lockRect.localScale = Vector3.one * startScale;

        float maxDur = Mathf.Max(bgFadeDuration, lockInDuration);
        while (tBg < bgFadeDuration || tLock < lockInDuration)
        {
            tBg = Mathf.Min(tBg + Time.deltaTime, bgFadeDuration);
            tLock = Mathf.Min(tLock + Time.deltaTime, lockInDuration);

            if (backgroundImage)
            {
                float u = bgFadeDuration <= 0f ? 1f : tBg / bgFadeDuration;
                SetImageAlpha(backgroundImage, Mathf.Lerp(startBg, endBg, fadeInCurve.Evaluate(u)));
            }
            if (lockImage && lockRect)
            {
                float v = lockInDuration <= 0f ? 1f : tLock / lockInDuration;
                SetImageAlpha(lockImage, Mathf.Lerp(startLockA, endLockA, fadeInCurve.Evaluate(v)));
                lockRect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, scaleInCurve.Evaluate(v));
            }

            if (tBg >= bgFadeDuration && tLock >= lockInDuration) break;
            yield return null;
        }

        if (backgroundImage) backgroundImage.raycastTarget = backgroundBlocksRaycastsWhenLocked;
        if (lockImage) lockImage.raycastTarget = backgroundBlocksRaycastsWhenLocked;

        _anim = null;
    }

    IEnumerator PlayUnlock()
    {
        float tBg = 0f;
        float startBg = GetAlpha(backgroundImage);
        float endBg = bgUnlockedAlpha;

        float tLock = 0f;
        float startScale = lockedScale;
        float endScale = unlockExitOverscale;
        float startLockA = GetAlpha(lockImage);
        float endLockA = lockUnlockedAlpha;

        if (lockRect) lockRect.localScale = Vector3.one * startScale;

        while (tBg < bgFadeDuration || tLock < lockOutDuration)
        {
            tBg = Mathf.Min(tBg + Time.deltaTime, bgFadeDuration);
            tLock = Mathf.Min(tLock + Time.deltaTime, lockOutDuration);

            if (backgroundImage)
            {
                float u = bgFadeDuration <= 0f ? 1f : tBg / bgFadeDuration;
                SetImageAlpha(backgroundImage, Mathf.Lerp(startBg, endBg, fadeOutCurve.Evaluate(u)));
            }
            if (lockImage && lockRect)
            {
                float v = lockOutDuration <= 0f ? 1f : tLock / lockOutDuration;
                SetImageAlpha(lockImage, Mathf.Lerp(startLockA, endLockA, fadeOutCurve.Evaluate(v)));
                lockRect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, scaleOutCurve.Evaluate(v));
            }

            if (tBg >= bgFadeDuration && tLock >= lockOutDuration) break;
            yield return null;
        }

        if (backgroundImage) backgroundImage.raycastTarget = false;
        if (lockImage) lockImage.raycastTarget = false;

        _anim = null;
    }

    static void SetImageAlpha(Image img, float a)
    {
        if (!img) return;
        var c = img.color; c.a = a; img.color = c;
    }
    static float GetAlpha(Image img) => img ? img.color.a : 0f;
}
