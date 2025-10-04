using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardBar : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] RectTransform listParent;
    [SerializeField] RectTransform entryPrefab;
    [SerializeField] float rowSpacing = 16f;
    [SerializeField] bool rebuildOnEnable = true;

    [Header("Effects (adjust here)")]
    [SerializeField] bool applyToName = true;
    [SerializeField] TMPGlowOutlineConfig nameEffects = new TMPGlowOutlineConfig();

    [SerializeField] bool applyToScore = false;
    [SerializeField] TMPGlowOutlineConfig scoreEffects = new TMPGlowOutlineConfig();

    [Header("Auto Size")]
    [SerializeField] ScoreboardRowAutoSizer autoSizer;

    void OnEnable() { if (rebuildOnEnable) Rebuild(); }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (!listParent || !entryPrefab) return;

        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        var names = (RosterStore.Instance != null) ? RosterStore.Instance.Names : null;
        if (names == null || names.Count == 0) return;

        var hlg = listParent.GetComponent<HorizontalLayoutGroup>();
        if (hlg)
        {
            hlg.spacing = rowSpacing;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = false;
        }

        foreach (var name in names)
        {
            var entry = Instantiate(entryPrefab, listParent);
            var refs = entry.GetComponent<ScoreboardEntryRefs>();
            if (!refs || !refs.nameTMP) continue;

            refs.nameTMP.text = name;
            if (refs.scoreTMP) refs.scoreTMP.text = "0";

            refs.nameTMP.color = Color.white;
            if (refs.scoreTMP) refs.scoreTMP.color = Color.white;

            Color effectColor = Color.white;
            RegistryNameColorLookup.TryGetColorForName(name, out effectColor);

            if (applyToName)
            {
                var fx = refs.nameTMP.GetComponent<TMPOutlineGlow>() ?? refs.nameTMP.gameObject.AddComponent<TMPOutlineGlow>();
                fx.SetConfig(nameEffects, false);
                fx.SetPerInstanceExplicitColor(effectColor, true);
            }

            if (applyToScore && refs.scoreTMP)
            {
                var fx = refs.scoreTMP.GetComponent<TMPOutlineGlow>() ?? refs.scoreTMP.gameObject.AddComponent<TMPOutlineGlow>();
                fx.SetConfig(scoreEffects, false);
                fx.SetPerInstanceExplicitColor(effectColor, true);
            }
        }

        if (!autoSizer) autoSizer = GetComponent<ScoreboardRowAutoSizer>();
        if (autoSizer) autoSizer.ResizeNow(listParent);
    }

    [ContextMenu("Reapply Effects")]
    public void ReapplyEffects()
    {
        var entries = listParent.GetComponentsInChildren<ScoreboardEntryRefs>(true);
        foreach (var e in entries)
        {
            if (applyToName && e.nameTMP)
            {
                var fx = e.nameTMP.GetComponent<TMPOutlineGlow>();
                if (fx) fx.SetConfig(nameEffects, true);
            }
            if (applyToScore && e.scoreTMP)
            {
                var fx = e.scoreTMP.GetComponent<TMPOutlineGlow>();
                if (fx) fx.SetConfig(scoreEffects, true);
            }
        }
    }
}
