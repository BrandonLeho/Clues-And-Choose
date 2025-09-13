using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class CoinGridSpawner : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Prefab with CoinMakerUI on the root")]
    public GameObject coinPrefab;

    [Tooltip("The GridLayoutGroup root (or any parent whose children are the slots)")]
    public Transform gridSlotsRoot;

    [Tooltip("Auto-discover slots as all direct children of gridSlotsRoot")]
    public bool autoDiscoverSlots = true;

    [Tooltip("Optionally clear any existing coins before spawning")]
    public bool clearExistingCoins = true;

    [Header("Timing")]
    [Tooltip("Call SpawnNow() automatically when this object enables")]
    public bool spawnOnEnable = false;

    readonly List<Transform> _slots = new List<Transform>();
    readonly List<CoinPlayerBinding> _spawned = new List<CoinPlayerBinding>();

    void OnEnable()
    {
        if (spawnOnEnable)
            StartCoroutine(WaitForRegistryThenSpawn());
        StartCoroutine(HookRegistryWhenReady());
    }

    IEnumerator WaitForRegistryThenSpawn()
    {
        float timeout = 3f;
        ColorLockRegistry reg = null;
        while (timeout > 0f && (reg = ColorLockRegistry.GetOrFind()) == null)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (reg != null) SpawnNow();
        else Debug.LogWarning("[CoinGridSpawner] Timed out waiting for ColorLockRegistry.");
    }


    IEnumerator HookRegistryWhenReady()
    {
        ColorLockRegistry reg = null;
        while ((reg = ColorLockRegistry.GetOrFind()) == null) yield return null;
        reg.OnRegistryChanged += RefreshAllColors;
    }

    void OnDisable()
    {
        var reg = ColorLockRegistry.GetOrFind();
        if (reg) reg.OnRegistryChanged -= RefreshAllColors;
    }

    public void SpawnNow()
    {
        if (!coinPrefab || !gridSlotsRoot) { Debug.LogWarning("..."); return; }

        BuildSlotList();

        var reg = ColorLockRegistry.GetOrFind();
        if (!reg) { Debug.LogWarning("No ColorLockRegistry in scene."); return; }

        var entries = reg.colorByOwner.ToArray(); // (ownerNetId -> Color)
        int playerCount = entries.Length;
        int need = Mathf.Min(_slots.Count, playerCount * 2);
        if (need == 0) return;

        // IMPORTANT: only the server should create and spawn networked coins
        if (!NetworkServer.active)
        {
            // Let the server do it; clients will get spawns automatically.
            return;
        }

        if (clearExistingCoins)
        {
            for (int i = 0; i < need; i++)
            {
                var slot = _slots[i];
                for (int c = slot.childCount - 1; c >= 0; c--)
                    Destroy(slot.GetChild(c).gameObject);
            }
        }

        _spawned.Clear();

        for (int i = 0; i < need; i++)
        {
            int playerIndex = i / 2;
            var entry = entries[playerIndex];
            uint ownerId = entry.Key;
            var slot = _slots[i];

            // Create coin under the intended slot (same hierarchy on server);
            // we'll also send a path in RPCs when we need to reparent.
            var coin = Instantiate(coinPrefab, slot, false);

            // Style/color
            var ui = coin.GetComponent<CoinMakerUI>();
            if (ui) ui.SetPlayerColor(entry.Value);

            // Bind owner id for local logic
            var bind = coin.GetComponent<CoinPlayerBinding>() ?? coin.AddComponent<CoinPlayerBinding>();
            bind.ownerNetId = ownerId;
            bind.ui = ui;
            bind.RefreshColor();
            _spawned.Add(bind);

            // Draggable knows the owner
            var dr = coin.GetComponent<DraggableCoin>();
            if (dr) dr.ownerNetId = ownerId;

            // NEW: network sync component also needs owner id
            var sync = coin.GetComponent<CoinDragSync>();
            if (sync) sync.ownerNetId = ownerId;

            // Spawn on network & grant client authority to the owner
            if (coin.TryGetComponent<NetworkIdentity>(out var ni))
            {
                // Find the owner's connection to grant authority
                if (NetworkServer.spawned.TryGetValue(ownerId, out var ownerIdentity) &&
                    ownerIdentity != null &&
                    ownerIdentity.connectionToClient != null)
                {
                    NetworkServer.Spawn(coin, ownerIdentity.connectionToClient);
                }
                else
                {
                    // Fallback: spawn without authority; the Commands still work because CoinDragSync validates sender.
                    NetworkServer.Spawn(coin);
                }
            }

            // Ensure uniform anchoring
            if (coin.transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }
        }
    }

    void BuildSlotList()
    {
        _slots.Clear();
        if (!gridSlotsRoot) return;
        for (int i = 0; i < gridSlotsRoot.childCount; i++)
            _slots.Add(gridSlotsRoot.GetChild(i));
    }

    void RefreshAllColors()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            var b = _spawned[i];
            if (!b) { _spawned.RemoveAt(i); continue; }
            b.RefreshColor();
        }

        SpawnNow();
    }
}
