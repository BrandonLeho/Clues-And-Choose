// Example: attach to the Lobby panel
using UnityEngine;
using TMPro;

public class PlayerListBinder : MonoBehaviour
{
    [SerializeField] Transform listParent;            // object with VerticalLayoutGroup
    [SerializeField] GameObject rowPrefab;            // prefab with a TMP_Text

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        foreach (Transform child in listParent) Destroy(child.gameObject);
        var names = SteamLobbySpace.LobbyUIManager.Instance.CurrentPlayerNames; // see helper above
        foreach (var name in names)
        {
            var row = Instantiate(rowPrefab, listParent);
            row.GetComponentInChildren<TMP_Text>().text = name;
        }
    }
}
