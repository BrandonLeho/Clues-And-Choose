using UnityEngine;
using TMPro;

public class PlayerListBinder : MonoBehaviour
{
    [SerializeField] Transform listParent;
    [SerializeField] GameObject rowPrefab;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        foreach (Transform child in listParent) Destroy(child.gameObject);

        var roster = (RosterStore.Instance != null && RosterStore.Instance.Names != null && RosterStore.Instance.Names.Count > 0)
            ? RosterStore.Instance.Names
            : SteamLobbySpace.LobbyUIManager.Instance.CurrentPlayerNames;

        foreach (var name in roster)
        {
            var row = Instantiate(rowPrefab, listParent);
            var label = row.GetComponentInChildren<TMP_Text>();
            label.text = name;

            var outlineBinder = row.GetComponentInChildren<PlayerNameOutlineBinder>(true);
            if (outlineBinder) outlineBinder.SetOwnerName(name);

            var badgeBinder = row.GetComponentInChildren<ClueGiverBadgeBinder>(true);
            if (badgeBinder) badgeBinder.SetOwnerName(name);
        }
    }
}
