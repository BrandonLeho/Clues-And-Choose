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
    public float safetyPaddingPerEntry = 6f;
    public float measureEpsilon = 0.1f;

    [Header("Apply")]
    public bool applyOnEnable = true;
    public bool applyOnRectChange = true;

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

        float containerWidth = parent.rect.width;
        int padLeft = hlg ? hlg.padding.left : 0;
        int padRight = hlg ? hlg.padding.right : 0;
        float available = containerWidth - padLeft - padRight - (extraLeftRightPadding * 2f);
        if (available <= 0f) return;

        var allTMPs = parent.GetComponentsInChildren<TMP_Text>(true);
        var originalAuto = new Dictionary<TMP_Text, bool>(allTMPs.Length);
        foreach (var t in allTMPs)
        {
            originalAuto[t] = t.enableAutoSizing;
            t.enableAutoSizing = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        float lo = minFontSize;
        float hi = Mathf.Max(minFontSize, maxFontSizeLimit);
        for (int iter = 0; iter < 18; iter++)
        {
            float mid = (lo + hi) * 0.5f;
            float needed = MeasureTotalWidth(entries, mid)
                         + (entryCount > 1 ? spacing * (entryCount - 1) : 0f)
                         + (safetyPaddingPerEntry * entryCount);

            if (needed <= available) lo = mid; else hi = mid;
            if (Mathf.Abs(hi - lo) <= measureEpsilon) break;
        }

        float finalSize = Mathf.Clamp(lo, minFontSize, maxFontSizeLimit);
        foreach (var t in allTMPs)
        {
            t.fontSize = finalSize;
            t.ForceMeshUpdate();
            t.enableAutoSizing = false;
        }
    }

    float MeasureTotalWidth(ScoreboardEntryRefs[] entries, float testSize)
    {
        float total = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (!e) continue;

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

            total += maxW;
        }

        return total;
    }
}
