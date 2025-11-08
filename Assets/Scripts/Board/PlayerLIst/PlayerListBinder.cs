using UnityEngine;
using TMPro;

public class PlayerListBinder : MonoBehaviour
{
    [SerializeField] Transform listParent;
    [SerializeField] GameObject rowPrefab;

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        foreach (Transform child in listParent) Destroy(child.gameObject);
        var names = SteamLobbySpace.LobbyUIManager.Instance.CurrentPlayerNames;
        foreach (var name in names)
        {
            var row = Instantiate(rowPrefab, listParent);
            var label = row.GetComponentInChildren<TMP_Text>();
            label.text = name;

            var outlineBinder = row.GetComponentInChildren<PlayerNameOutlineBinder>(true);
            if (outlineBinder) outlineBinder.SetOwnerName(name);
        }
    }
}
