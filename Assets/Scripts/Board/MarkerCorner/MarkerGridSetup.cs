using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MarkerGridSetup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform listParent;            // Has GridLayoutGroup
    [SerializeField] GameObject leftCellPrefab;       // Shows player name
    [SerializeField] GameObject rightCellPrefab;      // Optional (null = reuse left prefab)

    [Header("Data Source")]
    [Tooltip("Use the saved roster in the Game scene (RosterStore). If false, read from LobbyUIManager.")]
    [SerializeField] bool useRosterStore = true;

    [Header("Layout")]
    [Tooltip("Recalculate and rebuild on enable.")]
    [SerializeField] bool refreshOnEnable = true;
    [Tooltip("Recompute cell size to exactly fit 2 columns × (players) rows.")]
    [SerializeField] bool fitCellSizeToParent = true;
    [SerializeField] float minRowHeight = 0f;

    GridLayoutGroup grid;
    RectTransform parentRect;

    void Awake()
    {
        if (!listParent) listParent = transform;
        grid = listParent.GetComponent<GridLayoutGroup>();
        parentRect = listParent as RectTransform;

        if (!grid)
            Debug.LogError("[GridRosterBinder] listParent must have a GridLayoutGroup.");
    }

    void OnEnable()
    {
        if (refreshOnEnable) Refresh();
    }

    public void Refresh()
    {
        if (!grid) return;

        var names = GetNames();
        int playerCount = Mathf.Max(0, names.Count);
        int rows = Mathf.Max(1, playerCount);
        int columns = 2;

        // 1) Clear existing children
        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        // 2) Enforce 2 columns, rows = player count
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        // 3) Fit cell size so 2 × rows fills the parent nicely
        if (fitCellSizeToParent && parentRect)
        {
            var pad = grid.padding;
            float innerW = Mathf.Max(1f, parentRect.rect.width - pad.left - pad.right);
            float innerH = Mathf.Max(1f, parentRect.rect.height - pad.top - pad.bottom);

            float totalSpacingX = grid.spacing.x * (columns - 1);
            float totalSpacingY = grid.spacing.y * (rows - 1);

            float cellW = (innerW - totalSpacingX) / columns;
            float cellH = (innerH - totalSpacingY) / rows;
            if (minRowHeight > 0f) cellH = Mathf.Max(minRowHeight, cellH);

            grid.cellSize = new Vector2(Mathf.Max(1f, cellW), Mathf.Max(1f, cellH));
        }

        // 4) Spawn exactly 2 cells per player
        for (int i = 0; i < playerCount; i++)
        {
            // LEFT cell (name)
            var left = InstantiateSafeActive(leftCellPrefab ?? rightCellPrefab, listParent);
            EnableAllComponents(left);
            SetTextIfPresent(left, names[i]);

            // RIGHT cell (optional extra info or blank)
            var right = InstantiateSafeActive(rightCellPrefab ?? leftCellPrefab, listParent);
            EnableAllComponents(right);
            // Put whatever you want here; by default we leave it blank.
            // Example: SetTextIfPresent(right, $"#{i + 1}");
        }

        // If there are 0 players, you’ll see 1 row × 2 columns (empty cells won’t be created).
    }

    List<string> GetNames()
    {
        if (useRosterStore && RosterStore.Instance != null && RosterStore.Instance.Names != null)
            return RosterStore.Instance.Names;

        if (SteamLobbySpace.LobbyUIManager.Instance != null)
            return new List<string>(SteamLobbySpace.LobbyUIManager.Instance.CurrentPlayerNames);

        return new List<string>();
    }

    // --- Helpers ---

    static GameObject InstantiateSafeActive(GameObject prefab, Transform parent)
    {
        if (prefab == null)
        {
            var go = new GameObject("Cell");
            go.transform.SetParent(parent, false);
            go.SetActive(true);
            return go;
        }

        // Ensure instance is active even if prefab is inactive
        var instance = Object.Instantiate(prefab, parent);
        instance.SetActive(true);
        return instance;
    }

    static void SetTextIfPresent(GameObject root, string value)
    {
        // Prefer a specifically named text if you use one; otherwise first TMP_Text found.
        TMP_Text text = root.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.enabled = true;
            text.text = value ?? string.Empty;
        }
    }

    static void EnableAllComponents(GameObject root)
    {
        // Re-enable typical UI/behaviour components that might be disabled on the prefab.
        var behaviours = root.GetComponentsInChildren<Behaviour>(true);
        foreach (var b in behaviours)
        {
            // Don’t re-enable disabled scripts that you explicitly want off; if needed, add filters here.
            b.enabled = true;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = true;

        var colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = true;

        var colliders2D = root.GetComponentsInChildren<Collider2D>(true);
        foreach (var c2 in colliders2D) c2.enabled = true;

        // Also make sure all spawned objects are active
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);
    }
}
