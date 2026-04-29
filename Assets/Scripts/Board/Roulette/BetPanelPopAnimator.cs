using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BetPanelPopAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform panel;

    [Header("Positions (anchored)")]
    [SerializeField] Vector2 hiddenAnchoredPos = new Vector2(0f, -120f);

    [Header("Scale")]
    [SerializeField] float hiddenScale = 0.92f;

    [Header("Timing")]
    [SerializeField] float inDuration = 0.12f;
    [SerializeField] float outDuration = 0.10f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Behavior")]
    [SerializeField] bool disableRaycastsWhenHidden = true;

    [Header("Bet Choice Hover Logging")]
    [SerializeField] bool enableProbeHoverLogging = true;
    [SerializeField] bool logOnlyWhenBetPanelsActive = true;
    [SerializeField] GraphicRaycaster raycaster;
    [SerializeField] EventSystem eventSystem;

    [System.Serializable]
    public class TranslateTarget
    {
        public Transform target;

        [Header("Offsets (local space)")]
        public float xOffset = 0f;
        public float yOffset = 0f;

        [HideInInspector] public Vector3 originalPos;
    }

    [Header("Extra Objects to Translate")]
    [SerializeField] List<TranslateTarget> translateTargets = new List<TranslateTarget>();

    Coroutine _co;
    bool _isShown;

    bool _initialized;
    Vector2 _shownAnchoredPos;
    float _shownScale;
    readonly HashSet<GameObject> _betChoiceObjects = new HashSet<GameObject>();
    GameObject _currentProbeHover;

    void Reset()
    {
        panel = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (!raycaster) raycaster = GetComponentInParent<GraphicRaycaster>();
        if (!eventSystem) eventSystem = FindFirstObjectByType<EventSystem>();
    }

    void EnsureInitialized()
    {
        if (_initialized) return;

        if (!panel) panel = transform as RectTransform;
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (!raycaster) raycaster = GetComponentInParent<GraphicRaycaster>();
        if (!eventSystem) eventSystem = FindFirstObjectByType<EventSystem>();

        _shownAnchoredPos = panel.anchoredPosition;

        float currentScale = panel.localScale.x;
        _shownScale = Mathf.Approximately(currentScale, 0f) ? 1f : currentScale;

        foreach (var t in translateTargets)
        {
            if (t.target != null)
                t.originalPos = t.target.localPosition;
        }

        ConfigureBetChoiceHoverLoggers();

        _initialized = true;
    }

    void ConfigureBetChoiceHoverLoggers()
    {
        if (!enableProbeHoverLogging) return;

        _betChoiceObjects.Clear();

        var images = GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (!image || image.transform == transform) continue;

            if (!TryGetReadableChildLabel(image.transform, out _))
                continue;

            _betChoiceObjects.Add(image.gameObject);
            image.raycastTarget = true;
        }
    }

    void Update()
    {
        UpdateProbeHover();
    }

    void UpdateProbeHover()
    {
        if (!enableProbeHoverLogging || !_isShown)
        {
            ClearProbeHover();
            return;
        }

        if (logOnlyWhenBetPanelsActive && !RouletteBetHoverZones.BetPanelsActive)
        {
            ClearProbeHover();
            return;
        }

        var probe = CoinPlacementProbe.Active;
        if (probe == null || !raycaster || !eventSystem)
        {
            ClearProbeHover();
            return;
        }

        var pointerData = new PointerEventData(eventSystem) { position = probe.GetProbeScreenPosition() };
        var results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        GameObject target = null;
        for (int i = 0; i < results.Count; i++)
        {
            target = FindBetChoiceInParents(results[i].gameObject.transform);
            if (target) break;
        }

        if (target == _currentProbeHover)
            return;

        _currentProbeHover = target;
        if (_currentProbeHover)
            LogBetChoiceHover(_currentProbeHover);
    }

    GameObject FindBetChoiceInParents(Transform hit)
    {
        while (hit)
        {
            if (_betChoiceObjects.Contains(hit.gameObject))
                return hit.gameObject;

            if (hit == transform)
                break;

            hit = hit.parent;
        }

        return null;
    }

    void ClearProbeHover()
    {
        _currentProbeHover = null;
    }

    void LogBetChoiceHover(GameObject betChoice)
    {
        if (logOnlyWhenBetPanelsActive && !RouletteBetHoverZones.BetPanelsActive)
            return;

        string label = TryGetReadableChildLabel(betChoice.transform, out var labelText)
            ? labelText
            : betChoice.name;

        Debug.Log($"[RouletteBetChoice] Hovered: {label}", betChoice);
    }

    static bool TryGetReadableChildLabel(Transform root, out string labelText)
    {
        labelText = string.Empty;
        if (!root) return false;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);

            var tmp = child.GetComponent<TMP_Text>();
            if (tmp && !string.IsNullOrWhiteSpace(tmp.text))
            {
                labelText = tmp.text;
                return true;
            }

            var uiText = child.GetComponent<Text>();
            if (uiText && !string.IsNullOrWhiteSpace(uiText.text))
            {
                labelText = uiText.text;
                return true;
            }
        }

        return false;
    }

    void Awake()
    {
        EnsureInitialized();
        ApplyInstantHidden();
    }

    public void Show()
    {
        if (_isShown) return;
        _isShown = true;
        StartTween(true);
    }

    public void Hide()
    {
        if (!_isShown) return;
        _isShown = false;
        StartTween(false);
    }

    public void ApplyInstantHidden()
    {
        EnsureInitialized();

        panel.anchoredPosition = GetHiddenPos();
        panel.localScale = Vector3.one * (_shownScale * hiddenScale);
        canvasGroup.alpha = 0f;
        SetInteractable(false);
        _isShown = false;
    }

    void StartTween(bool toShown)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoTween(toShown));
    }

    IEnumerator CoTween(bool toShown)
    {
        EnsureInitialized();

        float d = Mathf.Max(0.0001f, toShown ? inDuration : outDuration);

        Vector2 p0 = panel.anchoredPosition;
        Vector2 p1 = toShown ? _shownAnchoredPos : GetHiddenPos();

        float s0 = panel.localScale.x;
        float s1 = toShown ? _shownScale : _shownScale * hiddenScale;

        float a0 = canvasGroup.alpha;
        float a1 = toShown ? 1f : 0f;

        SetInteractable(toShown);

        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            float e = ease != null ? ease.Evaluate(u) : u;

            panel.anchoredPosition = Vector2.LerpUnclamped(p0, p1, e);
            float s = Mathf.LerpUnclamped(s0, s1, e);
            panel.localScale = Vector3.one * s;
            canvasGroup.alpha = Mathf.LerpUnclamped(a0, a1, e);

            foreach (var tItem in translateTargets)
            {
                if (!tItem.target) continue;

                Vector3 offset = new Vector3(tItem.xOffset, tItem.yOffset, 0f);

                Vector3 start = toShown
                    ? tItem.originalPos
                    : tItem.originalPos + offset;

                Vector3 end = toShown
                    ? tItem.originalPos + offset
                    : tItem.originalPos;

                tItem.target.localPosition = Vector3.LerpUnclamped(start, end, e);
            }

            yield return null;
        }

        panel.anchoredPosition = p1;
        panel.localScale = Vector3.one * s1;
        canvasGroup.alpha = a1;

        if (!toShown) SetInteractable(false);
        _co = null;

        foreach (var tItem in translateTargets)
        {
            if (!tItem.target) continue;

            Vector3 offset = new Vector3(tItem.xOffset, tItem.yOffset, 0f);

            tItem.target.localPosition = toShown
                ? tItem.originalPos + offset
                : tItem.originalPos;
        }
    }

    Vector2 GetHiddenPos()
    {
        return _shownAnchoredPos + hiddenAnchoredPos;
    }

    void SetInteractable(bool on)
    {
        if (!disableRaycastsWhenHidden) return;
        canvasGroup.blocksRaycasts = on;
        canvasGroup.interactable = on;
    }
}
