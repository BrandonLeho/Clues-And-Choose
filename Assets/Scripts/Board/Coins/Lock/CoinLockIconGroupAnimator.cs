using System.Collections;
using UnityEngine;

public sealed class CoinLockIconGroupAnimator : MonoBehaviour
{
    [Header("Targets")]
    public GameObject iconGO;
    public SpriteRenderer iconRenderer;
    public GameObject backgroundGO;
    public SpriteRenderer backgroundRenderer;

    [Header("Icon Scale")]
    [Min(0f)] public float iconBaseScale = 1f;
    [Min(1f)] public float iconUpscaledMultiplier = 1.2f;

    [Header("Alpha")]
    [Range(0f, 1f)] public float iconBaseAlpha = 1f;
    [Range(0f, 1f)] public float bgBaseAlpha = 0.6f;

    [Header("Timing")]
    [Min(0.01f)] public float fadeInDuration = 0.20f;
    [Min(0.01f)] public float fadeOutDuration = 0.20f;
    [Min(0.01f)] public float scaleInDuration = 0.20f;
    [Min(0.01f)] public float scaleOutDuration = 0.20f;

    [Header("Ease")]
    [Range(0f, 1f)] public float easeInBias = 0.6f;
    [Range(0f, 1f)] public float easeOutBias = 0.6f;

    Coroutine _anim;
    bool _spawnSettled;
    bool _pendingLockShow;

    void Awake()
    {
        if (iconGO && !iconRenderer) iconRenderer = iconGO.GetComponent<SpriteRenderer>();
        if (backgroundGO && !backgroundRenderer) backgroundRenderer = backgroundGO.GetComponent<SpriteRenderer>();

        SetIconScale(iconBaseScale * iconUpscaledMultiplier);
        SetIconAlpha(0f);
        SetBgAlpha(0f);

        if (iconGO) iconGO.SetActive(false);
        if (backgroundGO) backgroundGO.SetActive(false);
    }

    void OnEnable()
    {
        CoinRoundLockManager.OnLocked += HandleLocked;
        CoinRoundLockManager.OnUnlocked += HandleUnlocked;
        CoinNetworkSpawner.OnInitialSpawnSettled += HandleSpawnSettled;
        GameRuleSettings.OnLockAllCoinsChanged += HandleRuleFlip;

        if (GameRuleSettings.IsLockAllEnabled)
        {
            if (_spawnSettled) PlayLockedIn();
            else _pendingLockShow = true;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        CoinRoundLockManager.OnLocked -= HandleLocked;
        CoinRoundLockManager.OnUnlocked -= HandleUnlocked;
        CoinNetworkSpawner.OnInitialSpawnSettled -= HandleSpawnSettled;
        GameRuleSettings.OnLockAllCoinsChanged -= HandleRuleFlip;
    }

    void HandleSpawnSettled()
    {
        _spawnSettled = true;

        if (_pendingLockShow || CoinRoundLockManager.IsLocked)
        {
            _pendingLockShow = false;
            PlayLockedIn();
        }
    }

    void HandleLocked()
    {
        if (!_spawnSettled)
        {
            _pendingLockShow = true;
            return;
        }

        PlayLockedIn();
    }

    void HandleUnlocked()
    {
        _pendingLockShow = false;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateOut());
    }

    void PlayLockedIn()
    {
        if (iconGO && !iconGO.activeSelf) iconGO.SetActive(true);
        if (backgroundGO && !backgroundGO.activeSelf) backgroundGO.SetActive(true);

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateIn());
    }

    IEnumerator AnimateIn()
    {
        float aIcon0 = iconRenderer ? iconRenderer.color.a : 0f;
        float aIcon1 = Mathf.Clamp01(iconBaseAlpha);
        float aBg0 = backgroundRenderer ? backgroundRenderer.color.a : 0f;
        float aBg1 = Mathf.Clamp01(bgBaseAlpha);

        float tA = 0f;
        float s0 = GetIconScaleX();
        float s1 = iconBaseScale;
        float tS = 0f;

        while (tA < fadeInDuration || tS < scaleInDuration)
        {
            if (tA < fadeInDuration)
            {
                tA += Time.deltaTime;
                float uA = Smooth01(tA / fadeInDuration, easeInBias);
                SetIconAlpha(Mathf.Lerp(aIcon0, aIcon1, uA));
                SetBgAlpha(Mathf.Lerp(aBg0, aBg1, uA));
            }

            if (tS < scaleInDuration)
            {
                tS += Time.deltaTime;
                float uS = Smooth01(tS / scaleInDuration, easeInBias);
                SetIconScale(Mathf.Lerp(s0, s1, uS));
            }

            yield return null;
        }
        _anim = null;
    }

    IEnumerator AnimateOut()
    {
        float aIcon0 = iconRenderer ? iconRenderer.color.a : 0f;
        float aBg0 = backgroundRenderer ? backgroundRenderer.color.a : 0f;

        float tA = 0f;
        float s0 = GetIconScaleX();
        float s1 = iconBaseScale * iconUpscaledMultiplier;
        float tS = 0f;

        while (tA < fadeOutDuration || tS < scaleOutDuration)
        {
            if (tA < fadeOutDuration)
            {
                tA += Time.deltaTime;
                float uA = Smooth01(tA / fadeOutDuration, easeOutBias);
                SetIconAlpha(Mathf.Lerp(aIcon0, 0f, uA));
                SetBgAlpha(Mathf.Lerp(aBg0, 0f, uA));
            }

            if (tS < scaleOutDuration)
            {
                tS += Time.deltaTime;
                float uS = Smooth01(tS / scaleOutDuration, easeOutBias);
                SetIconScale(Mathf.Lerp(s0, s1, uS));
            }

            yield return null;
        }

        if (iconGO) iconGO.SetActive(false);
        if (backgroundGO) backgroundGO.SetActive(false);
        _anim = null;
    }

    float Smooth01(float u, float bias)
    {
        u = Mathf.Clamp01(u);
        if (bias <= 0f) return u;
        float s = Mathf.SmoothStep(0f, 1f, u);
        return Mathf.Lerp(u, s, bias);
    }

    void SetIconAlpha(float a) { if (!iconRenderer) return; var c = iconRenderer.color; c.a = Mathf.Clamp01(a); iconRenderer.color = c; }
    void SetBgAlpha(float a) { if (!backgroundRenderer) return; var c = backgroundRenderer.color; c.a = Mathf.Clamp01(a); backgroundRenderer.color = c; }
    void SetIconScale(float s) { if (iconGO) iconGO.transform.localScale = new Vector3(s, s, 1f); }
    float GetIconScaleX() { return iconGO ? iconGO.transform.localScale.x : iconBaseScale; }

#if UNITY_EDITOR
    [ContextMenu("Preview: Locked (In)")] void _PreviewIn() { _spawnSettled = true; HandleLocked(); }
    [ContextMenu("Preview: Unlocked (Out)")] void _PreviewOut() { HandleUnlocked(); }
#endif

    void HandleRuleFlip(bool enabled)
    {
        if (enabled)
        {
            if (_spawnSettled) PlayLockedIn();
            else _pendingLockShow = true;
        }
        else
        {
            HandleUnlocked();
        }
    }
}
