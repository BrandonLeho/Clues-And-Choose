using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class ScoreboardRowAutoSizer : MonoBehaviour
{
    [Header("Sizing")]
    [Min(1f)] public float minFontSize = 18f;
    [Min(1f)] public float maxFontSizeLimit = 120f;
    public float extraLeftRightPadding = 16f;
    public float safetyPaddingPerEntry = 10f;
    public float measureEpsilon = 0.1f;

    [Header("Apply")]
    public bool applyOnEnable = true;
    public bool applyOnRectChange = true;

    [Header("Layout Integration")]
    public bool setEntryPreferredWidths = true;
    public bool normalizeHLGSettings = true;

    void OnEnable()
    {
        if (applyOnEnable) ResizeNow(null);
    }

    void OnRectTransformDimensionsChange()
    {
        if (applyOnRectChange) ResizeNow(null);
    }

    public void ResizeNow(Transform listParentOverride)
    {
        var parent = (listParentOverride as RectTransform) ?? (transform as RectTransform);
        if (!parent) return;

        var entries = parent.GetComponentsInChildren<ScoreboardEntryRefs>(true);
        if (entries == null || entries.Length == 0) return;

        var hlg = parent.GetComponent<HorizontalLayoutGroup>();
        float spacing = hlg ? hlg.spacing : 0f;
        int entryCount = entries.Length;

        if (normalizeHLGSettings && hlg)
        {
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = false;
        }

        float containerWidth = parent.rect.width;
        int padLeft = hlg ? hlg.padding.left : 0;
        int padRight = hlg ? hlg.padding.right : 0;
        float available = containerWidth - padLeft - padRight - (extraLeftRightPadding * 2f);
        if (available <= 0f) return;

        var allTMPs = parent.GetComponentsInChildren<TMP_Text>(true);
        var originalAuto = new Dictionary<TMP_Text, bool>(allTMPs.Length);
        var originalWrap = new Dictionary<TMP_Text, TextWrappingModes>(allTMPs.Length);
        var originalOverflow = new Dictionary<TMP_Text, TextOverflowModes>(allTMPs.Length);

        foreach (var t in allTMPs)
        {
            originalAuto[t] = t.enableAutoSizing;
            originalWrap[t] = t.textWrappingMode;
            originalOverflow[t] = t.overflowMode;

            t.enableAutoSizing = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        float lo = minFontSize;
        float hi = Mathf.Max(minFontSize, maxFontSizeLimit);

        for (int iter = 0; iter < 20; iter++)
        {
            float mid = (lo + hi) * 0.5f;
            float totalNeeded = MeasureTotalWidth(entries, mid)
                              + (entryCount > 1 ? spacing * (entryCount - 1) : 0f)
                              + (safetyPaddingPerEntry * entryCount);

            if (totalNeeded <= available) lo = mid; else hi = mid;
            if (Mathf.Abs(hi - lo) <= measureEpsilon) break;
        }

        float finalSize = Mathf.Clamp(lo, minFontSize, maxFontSizeLimit);

        foreach (var t in allTMPs)
        {
            t.fontSize = finalSize;
            t.ForceMeshUpdate();
            t.enableAutoSizing = false;
        }

        if (setEntryPreferredWidths)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (!e) continue;

                float width = MeasureEntryMaxWidth(e, finalSize) + safetyPaddingPerEntry;

                var entryRect = e.GetComponent<RectTransform>();
                if (!entryRect) continue;

                var le = entryRect.GetComponent<LayoutElement>();
                if (!le) le = entryRect.gameObject.AddComponent<LayoutElement>();

                le.minWidth = -1f;
                le.flexibleWidth = 0f;
                le.preferredWidth = width;
            }
        }

        foreach (var t in allTMPs)
        {
            t.enableAutoSizing = false;
            t.textWrappingMode = originalWrap[t];
            t.overflowMode = originalOverflow[t];
        }
    }

    float MeasureTotalWidth(ScoreboardEntryRefs[] entries, float testSize)
    {
        float total = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (!e) continue;
            total += MeasureEntryMaxWidth(e, testSize);
        }
        return total;
    }

    float MeasureEntryMaxWidth(ScoreboardEntryRefs e, float testSize)
    {
        float maxW = 0f;

        if (e.nameTMP)
        {
            float orig = e.nameTMP.fontSize;
            e.nameTMP.fontSize = testSize;
            e.nameTMP.ForceMeshUpdate();
            maxW = Mathf.Max(maxW, e.nameTMP.preferredWidth);
            e.nameTMP.fontSize = orig;
        }

        if (e.scoreTMP)
        {
            float orig = e.scoreTMP.fontSize;
            e.scoreTMP.fontSize = testSize;
            e.scoreTMP.ForceMeshUpdate();
            maxW = Mathf.Max(maxW, e.scoreTMP.preferredWidth);
            e.scoreTMP.fontSize = orig;
        }

        return maxW;
    }
}
