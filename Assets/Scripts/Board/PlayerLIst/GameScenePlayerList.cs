using UnityEngine;
using TMPro;

public class GameScenePlayerList : MonoBehaviour
{
    [SerializeField] Transform listParent; // VerticalLayoutGroup parent
    [SerializeField] GameObject rowPrefab;

    void Start()
    {
        foreach (Transform c in listParent) Destroy(c.gameObject);
        var names = RosterStore.Instance != null ? RosterStore.Instance.Names : null;
        if (names == null) return;
        foreach (var n in names)
        {
            var row = Instantiate(rowPrefab, listParent);
            row.GetComponentInChildren<TMP_Text>().text = n;
        }
    }
}
