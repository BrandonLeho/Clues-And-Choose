using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameGroupEntryAnimator : MonoBehaviour
{
    [Header("Root (optional)")]
    [Tooltip("Optional root CanvasGroup. Not required, but if present we keep it enabled and let children stage-in.")]
    public CanvasGroup rootGroup;

    [Header("Global Defaults")]
    public float defaultDuration = 0.45f;
    public AnimationCurve defaultEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("If true, we set targets invisible and/or inactive until their stage begins.")]
    public bool prepareTargetsHiddenOnAwake = true;

    [Header("Stages (edit in Inspector)")]
    public List<Stage> stages = new List<Stage>();

    [Header("Diagnostics")]
    public bool logProgress = false;

    bool _isPlaying;

    [System.Serializable]
    public class Stage
    {
        public string name = "Stage";
        [Tooltip("Delay before this stage begins (from when the previous stage started or finished; see StartMode).")]
        public float startDelay = 0f;

        public StartMode stageStartMode = StartMode.AfterPreviousFinishes;

        [Tooltip("If true, items in this stage play together (with optional per-item stagger). If false, they play sequentially.")]
        public bool playItemsInParallel = true;

        [Tooltip("Extra delay between items when playing in parallel (stagger) or sequential (gap).")]
        public float perItemOffset = 0.03f;

        public List<Item> items = new List<Item>();
    }

    public enum StartMode
    {
        /// <summary>Wait until the previous stage finishes, then apply startDelay.</summary>
        AfterPreviousFinishes,
        /// <summary>Start relative to previous stage start time (lets you overlap stages by using a negative startDelay).</summary>
        RelativeToPreviousStart
    }

    [System.Serializable]
    public class Item
    {
        public string note;

        [Tooltip("The object to animate in. Usually a UI element under the Game group.")]
        public GameObject target;

        [Tooltip("High-level preset. You can add more types later without changing the conductor.")]
        public Preset preset = Preset.FadeInCanvasGroup;

        [Tooltip("Override duration; 0 = use global default.")]
        public float durationOverride = 0f;

        public AnimationCurve easeOverride;

        [Tooltip("If true, the GameObject is SetActive(false) until the item’s animation begins, then SetActive(true).")]
        public bool deactivateUntilStage = true;

        [Tooltip("Extra delay applied on top of stage timing for this specific item.")]
        public float extraDelay = 0f;

        [Header("Preset Tunables")]
        [Tooltip("Only for SlideFromBottom/Top/Left/Right; in pixels.")]
        public float slideDistance = 200f;

        [Tooltip("Only for ScaleIn; starting scale factor.")]
        public float scaleFrom = 0.85f;

        [Tooltip("If target has a CanvasGroup, enable raycasts at the end.")]
        public bool enableRaycastsOnComplete = true;
    }

    public enum Preset
    {
        None,                 // Just enable (no tween). Useful for instant reveals.
        FadeInCanvasGroup,    // Requires/auto-adds CanvasGroup
        SlideFromBottom,      // Uses RectTransform (anchoredPosition)
        SlideFromTop,
        SlideFromLeft,
        SlideFromRight,
        ScaleIn               // localScale tween
    }

    void Awake()
    {
        if (prepareTargetsHiddenOnAwake)
            PrepareAllTargetsHidden();
    }

    /// <summary>Prime everything to a hidden/not-interactive state so we can reveal by stage.</summary>
    public void PrepareAllTargetsHidden()
    {
        foreach (var s in stages)
        {
            foreach (var it in s.items)
            {
                if (!it.target) continue;

                if (it.deactivateUntilStage)
                    it.target.SetActive(false);

                // Make invisible without changing active state (so layout stays stable if needed)
                switch (it.preset)
                {
                    case Preset.FadeInCanvasGroup:
                        {
                            var cg = GetOrAddCanvasGroup(it.target);
                            cg.alpha = 0f;
                            cg.interactable = false;
                            cg.blocksRaycasts = false;
                            break;
                        }
                    case Preset.SlideFromBottom:
                    case Preset.SlideFromTop:
                    case Preset.SlideFromLeft:
                    case Preset.SlideFromRight:
                        {
                            var rt = it.target.GetComponent<RectTransform>();
                            if (rt)
                            {
                                rt.anchoredPosition = GetSlideStart(rt, it);
                                SetAlphaIfCanvasGroup(it.target, 1f); // visible while offscreen
                            }
                            break;
                        }
                    case Preset.ScaleIn:
                        {
                            it.target.transform.localScale = Vector3.one * Mathf.Max(0.0001f, it.scaleFrom);
                            SetAlphaIfCanvasGroup(it.target, 1f);
                            break;
                        }
                    case Preset.None:
                    default:
                        {
                            SetAlphaIfCanvasGroup(it.target, 0f);
                            break;
                        }
                }
            }
        }
    }

    [ContextMenu("Play")]
    public void Play()
    {
        if (!_isPlaying) StartCoroutine(Co_Play());
    }

    public IEnumerator Co_Play()
    {
        _isPlaying = true;

        // One frame to ensure layout is settled before measuring RectTransforms
        yield return null;
        Canvas.ForceUpdateCanvases();

        float timelineStart = Time.unscaledTime;
        float lastStageEnd = timelineStart;

        for (int si = 0; si < stages.Count; si++)
        {
            var stage = stages[si];

            // Compute when this stage should start
            float baseStart =
                (stage.stageStartMode == StartMode.AfterPreviousFinishes ? lastStageEnd : timelineStart);
            float stageStartAt = baseStart + stage.startDelay;

            float now = Time.unscaledTime;
            if (stageStartAt > now)
                yield return new WaitForSecondsRealtime(stageStartAt - now);

            if (logProgress) Debug.Log($"[EntryAnimator] BEGIN Stage {si}: '{stage.name}'");

            if (stage.playItemsInParallel)
            {
                // Parallel with optional per-item offset (stagger)
                List<Coroutine> running = new List<Coroutine>();
                for (int i = 0; i < stage.items.Count; i++)
                {
                    var it = stage.items[i];
                    if (it.target == null) continue;

                    float delay = i * Mathf.Max(0f, stage.perItemOffset) + Mathf.Max(0f, it.extraDelay);
                    running.Add(StartCoroutine(Co_PlayItemDelayed(it, delay)));
                }
                // Wait for all
                foreach (var c in running) if (c != null) yield return c;
            }
            else
            {
                // Sequential
                for (int i = 0; i < stage.items.Count; i++)
                {
                    var it = stage.items[i];
                    if (it.target == null) continue;

                    float delay = Mathf.Max(0f, (i == 0 ? 0f : stage.perItemOffset)) + Mathf.Max(0f, it.extraDelay);
                    yield return Co_PlayItemDelayed(it, delay);
                }
            }

            lastStageEnd = Time.unscaledTime;
            if (logProgress) Debug.Log($"[EntryAnimator] END Stage {si}: '{stage.name}'");
        }

        _isPlaying = false;
    }

    IEnumerator Co_PlayItemDelayed(Item it, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (it.deactivateUntilStage) it.target.SetActive(true);

        float duration = it.durationOverride > 0f ? it.durationOverride : defaultDuration;
        var ease = (it.easeOverride != null && it.easeOverride.keys != null && it.easeOverride.keys.Length > 0)
            ? it.easeOverride
            : defaultEase;

        switch (it.preset)
        {
            case Preset.FadeInCanvasGroup:
                yield return Co_FadeIn(it.target, duration, ease, it.enableRaycastsOnComplete);
                break;

            case Preset.SlideFromBottom:
            case Preset.SlideFromTop:
            case Preset.SlideFromLeft:
            case Preset.SlideFromRight:
                yield return Co_SlideIn(it, duration, ease);
                break;

            case Preset.ScaleIn:
                yield return Co_ScaleIn(it, duration, ease);
                break;

            case Preset.None:
            default:
                // Instant enable + alpha 1 if CanvasGroup present
                SetAlphaIfCanvasGroup(it.target, 1f, it.enableRaycastsOnComplete);
                break;
        }
    }

    IEnumerator Co_FadeIn(GameObject go, float duration, AnimationCurve ease, bool enableRaycasts)
    {
        var cg = GetOrAddCanvasGroup(go);
        cg.interactable = false;
        cg.blocksRaycasts = false;

        float t = 0f;
        float start = cg.alpha;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            cg.alpha = Mathf.LerpUnclamped(start, 1f, ease.Evaluate(p));
            yield return null;
        }
        cg.alpha = 1f;
        if (enableRaycasts)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    IEnumerator Co_SlideIn(Item it, float duration, AnimationCurve ease)
    {
        var rt = it.target.GetComponent<RectTransform>();
        if (!rt)
        {
            // Fallback to simple fade if no RectTransform
            yield return Co_FadeIn(it.target, duration, ease, it.enableRaycastsOnComplete);
            yield break;
        }

        Vector2 end = rt.anchoredPosition;
        Vector2 start = GetSlideStart(rt, it);

        // Ensure at start and visible
        rt.anchoredPosition = start;
        SetAlphaIfCanvasGroup(it.target, 1f);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, ease.Evaluate(p));
            yield return null;
        }
        rt.anchoredPosition = end;

        var cg = it.target.GetComponent<CanvasGroup>();
        if (cg && it.enableRaycastsOnComplete)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    IEnumerator Co_ScaleIn(Item it, float duration, AnimationCurve ease)
    {
        var tr = it.target.transform;
        Vector3 end = Vector3.one;
        Vector3 start = Vector3.one * Mathf.Max(0.0001f, it.scaleFrom);

        tr.localScale = start;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            tr.localScale = Vector3.LerpUnclamped(start, end, ease.Evaluate(p));
            yield return null;
        }
        tr.localScale = end;

        var cg = it.target.GetComponent<CanvasGroup>();
        if (cg && it.enableRaycastsOnComplete)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    // Helpers
    static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    static void SetAlphaIfCanvasGroup(GameObject go, float a, bool enableRaycasts = false)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.alpha = a;
            if (enableRaycasts && a >= 1f)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
    }

    static Vector2 GetSlideStart(RectTransform rt, Item it)
    {
        float d = it.slideDistance <= 0f ? 200f : it.slideDistance;
        switch (it.preset)
        {
            case Preset.SlideFromBottom: return rt.anchoredPosition + new Vector2(0f, -d);
            case Preset.SlideFromTop: return rt.anchoredPosition + new Vector2(0f, d);
            case Preset.SlideFromLeft: return rt.anchoredPosition + new Vector2(-d, 0f);
            case Preset.SlideFromRight: return rt.anchoredPosition + new Vector2(d, 0f);
        }
        return rt.anchoredPosition;
    }
}
