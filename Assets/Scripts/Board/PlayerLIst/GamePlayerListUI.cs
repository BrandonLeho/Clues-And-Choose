using Mirror;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class GamePlayerListUI : MonoBehaviour
{
    public Transform listParent;
    public GameObject nameRowPrefab;
    readonly List<TextMeshProUGUI> rows = new();

    void OnEnable()
    {
        TryBuild();
        HookRosterEvents(true);
    }

    void OnDisable()
    {
        HookRosterEvents(false);
    }

    void HookRosterEvents(bool on)
    {
        var roster = LobbyRoster.Instance;
        if (roster == null) return;

        if (on)
            roster.players.OnChange += OnRosterChanged;  // <-- 3-arg version
        else
            roster.players.OnChange -= OnRosterChanged;
    }

    // Mirror 3-arg OnChange: (op, key, item)
    void OnRosterChanged(SyncIDictionary<ulong, string>.Operation op, ulong key, string item)
    {
        // Just repaint from the current dict
        Repaint(LobbyRoster.Instance.players);
    }

    void TryBuild()
    {
        var roster = LobbyRoster.Instance;
        if (roster == null) return;
        Repaint(roster.players);
    }

    void Repaint(IReadOnlyDictionary<ulong, string> players)
    {
        foreach (var r in rows) if (r) Destroy(r.gameObject);
        rows.Clear();

        foreach (var kvp in players)
        {
            TextMeshProUGUI label = null;

            if (nameRowPrefab != null)
            {
                var go = Instantiate(nameRowPrefab, listParent);
                label = go.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                var idx = rows.Count;
                if (idx < listParent.childCount)
                    label = listParent.GetChild(idx).GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (label != null)
            {
                label.text = kvp.Value; // display name
                rows.Add(label);
            }
        }
    }
}
