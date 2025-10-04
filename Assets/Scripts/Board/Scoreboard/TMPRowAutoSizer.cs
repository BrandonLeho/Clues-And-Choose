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

    [Header("No-Wrap Control")]
    public bool equalColumnMode = false;
    public bool enforceSingleLineNoWrap = true;

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
        var stash = new List<(TMP_Text t, bool auto, TextWrappingModes wrap, TextOverflowModes over)>(allTMPs.Length);

        foreach (var t in allTMPs)
        {
            stash.Add((t, t.enableAutoSizing, t.textWrappingMode, t.overflowMode));
            t.enableAutoSizing = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        float lo = minFontSize;
        float hi = Mathf.Max(minFontSize, maxFontSizeLimit);

        for (int iter = 0; iter < 22; iter++)
        {
            float mid = (lo + hi) * 0.5f;

            bool fits = equalColumnMode
                ? FitsEqualColumns(entries, mid, available, spacing)
                : FitsMeasuredWidths(entries, mid, available, spacing);

            if (fits) lo = mid; else hi = mid;
            if (Mathf.Abs(hi - lo) <= measureEpsilon) break;
        }

        float finalSize = Mathf.Clamp(lo, minFontSize, maxFontSizeLimit);

        foreach (var t in allTMPs)
        {
            t.fontSize = finalSize;
            t.ForceMeshUpdate();
            t.enableAutoSizing = false;
            if (enforceSingleLineNoWrap)
            {
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow;
            }
        }

        if (setEntryPreferredWidths)
        {
            if (equalColumnMode)
            {
                float totalSpacing = entryCount > 1 ? spacing * (entryCount - 1) : 0f;
                float per = Mathf.Max(0f, (available - totalSpacing) / entryCount) - safetyPaddingPerEntry;

                for (int i = 0; i < entries.Length; i++)
                    EnsureLE(entries[i]).preferredWidth = Mathf.Max(0f, per);
            }
            else
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    float w = MeasureEntryMaxWidth(entries[i], finalSize) + safetyPaddingPerEntry;
                    EnsureLE(entries[i]).preferredWidth = Mathf.Max(0f, w);
                }
            }
        }

        foreach (var s in stash)
        {
            s.t.enableAutoSizing = false;
        }
    }

    bool FitsMeasuredWidths(ScoreboardEntryRefs[] entries, float testSize, float available, float spacing)
    {
        float sum = 0f;
        for (int i = 0; i < entries.Length; i++)
            sum += MeasureEntryMaxWidth(entries[i], testSize) + safetyPaddingPerEntry;

        float totalNeeded = sum + (entries.Length > 1 ? spacing * (entries.Length - 1) : 0f);
        return totalNeeded <= available + 0.01f;
    }

    bool FitsEqualColumns(ScoreboardEntryRefs[] entries, float testSize, float available, float spacing)
    {
        int n = entries.Length;
        float totalSpacing = n > 1 ? spacing * (n - 1) : 0f;
        float per = (available - totalSpacing) / Mathf.Max(1, n);
        float target = Mathf.Max(0f, per - safetyPaddingPerEntry);

        for (int i = 0; i < n; i++)
        {
            float need = MeasureEntryMaxWidth(entries[i], testSize);
            if (need > target + 0.01f) return false;
        }
        return true;
    }

    float MeasureEntryMaxWidth(ScoreboardEntryRefs e, float testSize)
    {
        float maxW = 0f;

        if (e && e.nameTMP)
        {
            float orig = e.nameTMP.fontSize;
            e.nameTMP.fontSize = testSize;
            e.nameTMP.ForceMeshUpdate();
            maxW = Mathf.Max(maxW, e.nameTMP.preferredWidth);
            e.nameTMP.fontSize = orig;
        }
        if (e && e.scoreTMP)
        {
            float orig = e.scoreTMP.fontSize;
            e.scoreTMP.fontSize = testSize;
            e.scoreTMP.ForceMeshUpdate();
            maxW = Mathf.Max(maxW, e.scoreTMP.preferredWidth);
            e.scoreTMP.fontSize = orig;
        }

        return maxW;
    }

    LayoutElement EnsureLE(ScoreboardEntryRefs e)
    {
        var r = e ? e.GetComponent<RectTransform>() : null;
        var le = r ? r.GetComponent<LayoutElement>() : null;
        if (!le && r) le = r.gameObject.AddComponent<LayoutElement>();
        if (le)
        {
            le.minWidth = -1f;
            le.flexibleWidth = 0f;
        }
        return le;
    }
}
