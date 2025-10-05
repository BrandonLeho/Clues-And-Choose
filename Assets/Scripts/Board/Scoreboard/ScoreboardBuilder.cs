using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardBuilder : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] Transform listParent;
    [SerializeField] GameObject bannerEntryPrefab;
    [SerializeField] bool clearOnBuild = true;

    [Header("Populate From")]
    [SerializeField] bool useRosterStore = true;

    public void Build(IReadOnlyList<string> namesOverride = null)
    {
        if (clearOnBuild && listParent != null)
        {
            for (int i = listParent.childCount - 1; i >= 0; i--)
                Destroy(listParent.GetChild(i).gameObject);
        }

        List<string> names = null;
        if (namesOverride != null)
            names = new List<string>(namesOverride);
        else if (useRosterStore && RosterStore.Instance != null)
            names = new List<string>(RosterStore.Instance.Names);
        else
            names = new List<string>();

        foreach (var name in names)
        {
            var go = Instantiate(bannerEntryPrefab, listParent);
            var entry = go.GetComponent<ScoreBannerEntry>();
            if (entry != null)
            {
                entry.Initialize(name);
            }
        }
    }

    void OnEnable()
    {
        Build();
    }
}
