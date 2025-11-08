using UnityEngine;
using TMPro;

public class GameScenePlayerList : MonoBehaviour
{
    [SerializeField] Transform listParent;
    [SerializeField] GameObject rowPrefab;

    void Start()
    {
        foreach (Transform c in listParent) Destroy(c.gameObject);
        var names = RosterStore.Instance != null ? RosterStore.Instance.Names : null;
        if (names == null) return;
        foreach (var n in names)
        {
            var row = Instantiate(rowPrefab, listParent);
            var label = row.GetComponentInChildren<TMP_Text>();
            label.text = n;

            var outlineBinder = row.GetComponentInChildren<PlayerNameOutlineBinder>(true);
            if (outlineBinder) outlineBinder.SetOwnerName(n);

            var badgeBinder = row.GetComponentInChildren<ClueGiverBadgeBinder>(true);
            if (badgeBinder) badgeBinder.SetOwnerName(n);
        }
    }
}
