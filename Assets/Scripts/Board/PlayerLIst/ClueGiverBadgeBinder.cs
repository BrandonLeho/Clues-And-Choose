using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class ClueGiverBadgeBinder : MonoBehaviour
{
    [SerializeField] string ownerName;

    [Header("If left empty")]
    [SerializeField] TMP_Text nameLabel;

    [Header("Badge Style")]
    [SerializeField] string badgeText = "CLUE GIVER";
    [SerializeField, Min(0.1f)] float fontSizeScale = 0.7f;
    [SerializeField] FontStyles fontStyle = FontStyles.Bold;
    [SerializeField] TextAlignmentOptions alignment = TextAlignmentOptions.Left;
    [SerializeField] Vector2 anchoredOffset = new Vector2(0f, -6f);

    [Header("Outline Control")]
    [SerializeField] bool enableOutline = true;

    [Header("Rect Size Override")]
    [SerializeField] bool overrideSize = false;
    [SerializeField] Vector2 sizeDelta = new Vector2(0f, 24f);

    [Header("Animation")]
    [SerializeField] float slideInDuration = 0.20f;
    [SerializeField] float slideOutDuration = 0.18f;
    [SerializeField] AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] float nameScaleMultiplier = 1.06f;
    [SerializeField] float nameScaleDuration = 0.18f;
    [SerializeField] AnimationCurve nameScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    TMP_Text _badge;
    Outline _nameOutlineUI;
    Outline _badgeOutlineUI;
    CanvasGroup _badgeCg;

    bool _initialized;
    bool _lastIsClueGiver;
    Coroutine _badgeAnimCo;
    Coroutine _nameScaleCo;

    int _badgeAnimVersion = 0;
    int _nameScaleVersion = 0;

    void Reset()
    {
        if (!nameLabel) nameLabel = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        TryInit();
        RosterStore.OnClueGiverChanged += HandleClueGiverChanged;
        Refresh();
    }

    void OnDisable()
    {
        RosterStore.OnClueGiverChanged -= HandleClueGiverChanged;
        StopAndNull(ref _badgeAnimCo);
        StopAndNull(ref _nameScaleCo);
    }

    public void SetOwnerName(string name)
    {
        ownerName = name;
        Refresh();
    }

    void HandleClueGiverChanged(string _) => Refresh();

    void TryInit()
    {
        if (_initialized) return;

        if (!nameLabel) nameLabel = GetComponentInChildren<TMP_Text>(true);
        if (!nameLabel) return;

        if (!_badge)
        {
            var parent = nameLabel.transform.parent != null ? nameLabel.transform.parent : transform;
            var go = new GameObject("ClueGiverLabel", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            _badge = go.AddComponent<TextMeshProUGUI>();

            _badgeCg = go.AddComponent<CanvasGroup>();
            _badgeCg.alpha = 0f;

            var nameTMP = nameLabel as TextMeshProUGUI;
            _badge.font = nameTMP ? nameTMP.font : _badge.font;
            _badge.fontSize = Mathf.Max(1f, nameLabel.fontSize * fontSizeScale);
            _badge.fontStyle = fontStyle;
            _badge.alignment = alignment;
            _badge.enableAutoSizing = false;
            _badge.raycastTarget = false;
            _badge.text = badgeText;

            int nameIdx = nameLabel.transform.GetSiblingIndex();
            _badge.transform.SetSiblingIndex(Mathf.Min(nameIdx + 1, parent.childCount - 1));

            var nrt = (RectTransform)nameLabel.transform;
            var brt = (RectTransform)_badge.transform;
            brt.anchorMin = nrt.anchorMin;
            brt.anchorMax = nrt.anchorMax;
            brt.pivot = nrt.pivot;
            brt.anchoredPosition = nrt.anchoredPosition;

            _nameOutlineUI = nameLabel.GetComponent<Outline>();
            _badgeOutlineUI = _badge.GetComponent<Outline>();
            if (_nameOutlineUI && !_badgeOutlineUI) _badgeOutlineUI = _badge.gameObject.AddComponent<Outline>();
        }

        _initialized = true;
    }

    void Refresh()
    {
        TryInit();
        if (!_badge) return;

        bool isClueGiver =
            !string.IsNullOrWhiteSpace(ownerName) &&
            string.Equals(ownerName, RosterStore.CurrentClueGiverName, System.StringComparison.Ordinal);

        if (nameLabel) _badge.color = nameLabel.color;

        SyncOutlineFromNameToBadge();
        ApplySizeOverride();

        if (isClueGiver == _lastIsClueGiver)
        {
            if (!_badge.gameObject.activeSelf && isClueGiver)
                _badge.gameObject.SetActive(true);
            return;
        }

        _lastIsClueGiver = isClueGiver;

        AnimateBadge(isClueGiver);
        AnimateNameScale(isClueGiver);
    }

    void AnimateBadge(bool show)
    {
        StopAndNull(ref _badgeAnimCo);
        _badgeAnimVersion++;
        _badgeAnimCo = StartCoroutine(Co_AnimateBadge(show, _badgeAnimVersion));
    }

    IEnumerator Co_AnimateBadge(bool show, int version)
    {
        if (!_badgeCg) _badgeCg = _badge.gameObject.GetComponent<CanvasGroup>() ?? _badge.gameObject.AddComponent<CanvasGroup>();

        var nrt = (RectTransform)nameLabel.transform;
        var brt = (RectTransform)_badge.transform;

        Vector2 basePos = nrt.anchoredPosition;
        Vector2 startPos = show ? basePos : basePos + anchoredOffset;
        Vector2 endPos = show ? (basePos + anchoredOffset) : basePos;

        float startAlpha = show ? 0f : _badgeCg.alpha;
        float endAlpha = show ? 1f : 0f;

        float dur = show ? slideInDuration : slideOutDuration;
        if (dur <= 0f) dur = 0.001f;

        if (show)
        {
            _badge.gameObject.SetActive(true);
            _badgeCg.alpha = startAlpha;
            brt.anchoredPosition = startPos;
        }

        float t = 0f;
        while (t < dur)
        {
            if (version != _badgeAnimVersion) yield break;
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = slideCurve != null ? slideCurve.Evaluate(u) : u;

            brt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
            _badgeCg.alpha = Mathf.LerpUnclamped(startAlpha, endAlpha, e);
            yield return null;
        }

        if (version != _badgeAnimVersion) yield break;

        brt.anchoredPosition = endPos;
        _badgeCg.alpha = endAlpha;

        if (!show)
            _badge.gameObject.SetActive(false);
    }

    void AnimateNameScale(bool up)
    {
        StopAndNull(ref _nameScaleCo);
        _nameScaleVersion++;
        _nameScaleCo = StartCoroutine(Co_AnimateNameScale(up, _nameScaleVersion));
    }

    IEnumerator Co_AnimateNameScale(bool up, int version)
    {
        var tr = nameLabel.transform as RectTransform;
        float dur = nameScaleDuration <= 0f ? 0.001f : nameScaleDuration;

        Vector3 from = tr.localScale;
        Vector3 to = up ? Vector3.one * nameScaleMultiplier : Vector3.one;

        float t = 0f;
        while (t < dur)
        {
            if (version != _nameScaleVersion) yield break;
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = nameScaleCurve != null ? nameScaleCurve.Evaluate(u) : u;
            tr.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }

        if (version != _nameScaleVersion) yield break;
        tr.localScale = to;
    }

    void ApplySizeOverride()
    {
        if (!_badge) return;
        var rt = (RectTransform)_badge.transform;
        if (overrideSize) rt.sizeDelta = sizeDelta;
    }

    void SyncOutlineFromNameToBadge()
    {
        var nameTMP = nameLabel as TextMeshProUGUI;
        var badgeTMP = _badge as TextMeshProUGUI;

        if (!enableOutline)
        {
            if (badgeTMP)
            {
                var dst = badgeTMP.fontMaterial;
                if (dst != null && dst.HasProperty("_OutlineWidth"))
                    dst.SetFloat("_OutlineWidth", 0f);
            }
            if (_badgeOutlineUI) _badgeOutlineUI.enabled = false;
            return;
        }

        if (nameTMP && badgeTMP)
        {
            var src = nameTMP.fontMaterial;
            if (src != null)
            {
                var dst = badgeTMP.fontMaterial;
                if (dst == null || dst.shader != src.shader)
                {
                    dst = new Material(src);
                    badgeTMP.fontSharedMaterial = dst;
                }

                if (src.HasProperty("_OutlineColor") && dst.HasProperty("_OutlineColor"))
                    dst.SetColor("_OutlineColor", src.GetColor("_OutlineColor"));

                if (src.HasProperty("_OutlineWidth") && dst.HasProperty("_OutlineWidth"))
                    dst.SetFloat("_OutlineWidth", src.GetFloat("_OutlineWidth"));
            }
        }

        if (_nameOutlineUI)
        {
            if (!_badgeOutlineUI) _badgeOutlineUI = _badge.gameObject.AddComponent<Outline>();
            _badgeOutlineUI.enabled = true;
            _badgeOutlineUI.effectColor = _nameOutlineUI.effectColor;
            _badgeOutlineUI.effectDistance = _nameOutlineUI.effectDistance;
            _badgeOutlineUI.useGraphicAlpha = _nameOutlineUI.useGraphicAlpha;
        }
        else
        {
            if (_badgeOutlineUI) _badgeOutlineUI.enabled = false;
        }
    }

    void StopAndNull(ref Coroutine co)
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }
    }
}
