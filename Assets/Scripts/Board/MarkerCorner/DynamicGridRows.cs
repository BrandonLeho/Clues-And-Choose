using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DynamicGridRows : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform listParent;       // Parent with GridLayoutGroup
    [SerializeField] GameObject rowPrefab;       // Prefab containing a TMP_Text

    [Header("Behaviour")]
    [SerializeField] bool useRosterStore = true; // Use roster from game scene
    [SerializeField] bool refreshOnEnable = true;

    GridLayoutGroup grid;
    RectTransform parentRect;

    void Awake()
    {
        if (!listParent) listParent = transform;
        grid = listParent.GetComponent<GridLayoutGroup>();
        parentRect = listParent as RectTransform;

        if (!grid)
        {
            Debug.LogError("[DynamicGridRows] listParent must have a GridLayoutGroup.");
        }
    }

    void OnEnable()
    {
        if (refreshOnEnable) Refresh();
    }

    public void Refresh()
    {
        if (!grid) return;

        var names = GetNames();
        int playerCount = names.Count;

        // Clear existing children
        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        // Force 2 columns
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        // Row count = number of players
        int rows = Mathf.Max(1, playerCount);

        // Resize cells to fit 2 columns × playerCount rows
        if (parentRect != null)
        {
            var pad = grid.padding;
            float innerHeight = parentRect.rect.height - pad.top - pad.bottom;
            float innerWidth = parentRect.rect.width - pad.left - pad.right;

            float cellH = (innerHeight - grid.spacing.y * (rows - 1)) / rows;
            float cellW = (innerWidth - grid.spacing.x * (grid.constraintCount - 1)) / grid.constraintCount;

            grid.cellSize = new Vector2(Mathf.Max(1f, cellW), Mathf.Max(1f, cellH));
        }

        // Instantiate player entries
        foreach (var name in names)
        {
            var row = Instantiate(rowPrefab, listParent);
            var txt = row.GetComponentInChildren<TMP_Text>(true);
            if (txt) txt.text = name;
        }
    }

    List<string> GetNames()
    {
        if (useRosterStore && RosterStore.Instance != null && RosterStore.Instance.Names != null)
            return RosterStore.Instance.Names;

        if (SteamLobbySpace.LobbyUIManager.Instance != null)
            return new List<string>(SteamLobbySpace.LobbyUIManager.Instance.CurrentPlayerNames);

        return new List<string>();
    }
}
