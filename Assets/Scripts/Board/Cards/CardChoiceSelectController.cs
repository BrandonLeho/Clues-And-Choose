using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class CardChoiceSelectController : MonoBehaviour
{
    [Header("Choices (children)")]
    public List<ChoiceClickRelay> choices = new List<ChoiceClickRelay>();

    [Header("Focus Target")]
    public RectTransform focusTarget;

    [Header("Animation")]
    [Range(1f, 5f)] public float selectedScale = 1.18f;
    public float moveDuration = 0.35f;
    public float fadeDuration = 0.25f;
    [Range(0f, 1f)] public float easeOut = 0.7f;

    [Header("Behavior")]
    public bool disableHoverRelayOnSelect = true;
    public bool disableFurtherInputOnSelect = true;

    [Header("Layout Placeholder")]
    public bool usePlaceholderOnSelect = true;
    public bool removePlaceholderAfterSelect = false;

    [Serializable]
    public struct ChoicePayload
    {
        public int col;
        public int row;
        public Color color;
        public string label;
    }

    [Header("Events")]
    public UnityEvent<ChoicePayload> onChoiceSelected;

    RectTransform rt;
    bool locked;
    public bool HasLockedSelection => locked;

    RectTransform placeholder;
    int placeholderIndex = -1;
    public RectTransform floatingLayer;

    void Reset() => AutoFindChoices();

    void Awake()
    {
        rt = (RectTransform)transform;
        if (!focusTarget) focusTarget = rt;

        foreach (var ch in choices)
            if (ch) ch.controller = this;
    }

    [ContextMenu("Auto-Find Choices")]
    public void AutoFindChoices()
    {
        choices.Clear();
        foreach (Transform child in transform)
        {
            var relay = child.GetComponent<ChoiceClickRelay>();
            if (!relay) relay = child.gameObject.AddComponent<ChoiceClickRelay>();
            relay.controller = this;
            choices.Add(relay);
        }
    }

    public void Select(ChoiceClickRelay clicked, int col, int row, Color color)
    {
        if (locked) return;
        locked = disableFurtherInputOnSelect;

        if (disableHoverRelayOnSelect)
        {
            var relay = GridHoverRelay.Instance;
            if (relay) relay.HoverExit();
        }

        var selRT = (RectTransform)clicked.transform;
        if (usePlaceholderOnSelect)
        {
            CreateOrUpdatePlaceholder(selRT);
        }

        var worldPos = selRT.position;
        CreateOrUpdatePlaceholder(selRT);
        selRT.SetParent(floatingLayer, worldPositionStays: true);
        SetAnchorsPivotToCenter_KeepVisualPosition(selRT);

        clicked.transform.SetAsLastSibling();

        StartCoroutine(CoAnimateSelection(clicked, col, row, color));
    }

    IEnumerator CoAnimateSelection(ChoiceClickRelay selected, int col, int row, Color color)
    {
        foreach (var ch in choices)
        {
            if (!ch || ch == selected) continue;
            StartCoroutine(CoFade(ch.cg, ch.cg.alpha, 0f, fadeDuration));
            if (ch.cg) ch.cg.blocksRaycasts = false;
            if (ch.img) ch.img.raycastTarget = false;
        }

        var scaleT = GetScaleTarget(selected);
        var selRT = (RectTransform)selected.transform;

        Vector2 endPos = Vector2.zero;
        Vector2 startPos = selRT.anchoredPosition;
        Vector3 startScale = scaleT.localScale;
        Vector3 endScale = startScale * selectedScale;

        float t = 0f;
        float d = Mathf.Max(0.0001f, moveDuration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / d);
            float k = Mathf.Lerp(0f, 3f, easeOut);
            float eased = 1f - Mathf.Pow(1f - p, k + 1f);

            selRT.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
            scaleT.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        selRT.anchoredPosition = endPos;
        scaleT.localScale = endScale;
        FreezeHoverScale(selected, scaleT, endScale);
        AllowHoverOnSelected(selected);

        var hover = selected.GetComponent<ChoiceHoverScale>();
        if (hover) hover.enabled = false;

        var payload = new ChoicePayload { col = col, row = row, color = color, label = "" };
        onChoiceSelected?.Invoke(payload);

        if (disableFurtherInputOnSelect)
        {
            foreach (var ch in choices)
                if (ch && ch.cg && ch != selected)
                    ch.cg.blocksRaycasts = false;
        }

        if (removePlaceholderAfterSelect)
        {
            ClearPlaceholder();
        }
    }

    IEnumerator CoFade(CanvasGroup cg, float from, float to, float duration)
    {
        if (!cg) yield break;
        float t = 0f, d = Mathf.Max(0.0001f, duration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / d);
            cg.alpha = Mathf.Lerp(from, to, p);
            yield return null;
        }
        cg.alpha = to;
    }

    Transform GetScaleTarget(ChoiceClickRelay r)
    {
        if (r != null && r.img != null && r.img.transform is RectTransform)
            return r.img.transform;
        return r.transform;
    }

    void AllowHoverOnSelected(ChoiceClickRelay ch)
    {
        if (!ch) return;

        var gridHover = ch.GetComponent<CardChoiceToGridHover>();
        if (gridHover) gridHover.enabled = true;

        var hoverScale = ch.GetComponent<ChoiceHoverScale>();
        if (hoverScale) hoverScale.enabled = false;

        if (ch.cg) ch.cg.blocksRaycasts = true;
        if (ch.img) ch.img.raycastTarget = true;
    }

    void FreezeHoverScale(ChoiceClickRelay ch, Transform scaleT, Vector3 lockedScale)
    {
        if (!ch) return;

        var hovers = ch.GetComponentsInChildren<ChoiceHoverScale>(true);
        foreach (var h in hovers)
        {
            h.StopAllCoroutines();
            h.enabled = false;
        }

        if (scaleT) scaleT.localScale = lockedScale;
    }

    void CreateOrUpdatePlaceholder(RectTransform selected)
    {
        if (!selected) return;

        if (placeholder) ClearPlaceholder();

        placeholderIndex = selected.GetSiblingIndex();

        var go = new GameObject("Placeholder", typeof(RectTransform));
        placeholder = go.GetComponent<RectTransform>();
        placeholder.SetParent(selected.parent, worldPositionStays: false);
        placeholder.SetSiblingIndex(placeholderIndex);

        placeholder.anchorMin = selected.anchorMin;
        placeholder.anchorMax = selected.anchorMax;
        placeholder.pivot = selected.pivot;
        placeholder.sizeDelta = selected.sizeDelta;
        placeholder.anchoredPosition3D = selected.anchoredPosition3D;

    }

    public void ClearPlaceholder()
    {
        if (placeholder)
        {
            if (placeholder.gameObject) Destroy(placeholder.gameObject);
            placeholder = null;
        }
        placeholderIndex = -1;
    }

    static void SetAnchorsPivotToCenter_KeepVisualPosition(RectTransform rt)
    {
        if (!rt || !(rt.parent is RectTransform parent)) return;

        Vector3[] wc = new Vector3[4];
        rt.GetWorldCorners(wc);
        Vector3 worldCenter = (wc[0] + wc[2]) * 0.5f;
        Vector2 size = rt.rect.size;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        rt.position = worldCenter;
        var p = rt.anchoredPosition3D;
        rt.anchoredPosition3D = new Vector3(p.x, p.y, 0f);
    }

}
