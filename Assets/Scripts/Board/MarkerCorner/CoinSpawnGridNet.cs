// CoinSpawnGridNet.cs
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CoinSpawnGrid))]
public class CoinSpawnGridNet : NetworkBehaviour
{
    [Header("Prefab & Scene Hooks")]
    [Tooltip("UI coin prefab. Must have NetworkIdentity + NetworkTransform.")]
    [SerializeField] GameObject coinPrefab;

    [Tooltip("Optional: mark the slots root with tag 'CoinSlotsRoot' for client-side parenting.")]
    [SerializeField] string slotsRootTag = "CoinSlotsRoot";

    CoinSpawnGrid grid;
    List<Transform> slots = new List<Transform>();

    void Awake()
    {
        grid = GetComponent<CoinSpawnGrid>();
        BuildSlotsList();
    }

    void BuildSlotsList()
    {
        slots.Clear();
        // Mirror doesn't sync parenting; gather slots by walking the GridLayoutGroup
        var gridLayout = GetComponentsInChildren<GridLayoutGroup>(true).FirstOrDefault();
        var slotsRoot = gridLayout ? gridLayout.transform : transform;
        for (int i = 0; i < slotsRoot.childCount; i++)
            slots.Add(slotsRoot.GetChild(i));
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Option A: hook your existing phase event on the server only
        var phase = FindFirstObjectByType<ColorChoosingPhaseController>();
        if (phase) phase.onPhaseEnded.AddListener(SpawnAllServer);

        // Or call SpawnAllServer() directly whenever you're ready on the server.
    }

    [Server]
    public void SpawnAllServer()
    {
        var registry = ColorLockRegistry.GetOrFind();
        if (!registry || !coinPrefab) return;

        // Order owners like your grid script does
        var owners = registry.colorByOwner.Keys.ToList();
        owners.Sort((a, b) =>
        {
            registry.indexByOwner.TryGetValue(a, out var ia);
            registry.indexByOwner.TryGetValue(b, out var ib);
            return ia.CompareTo(ib);
        });

        int coinsPerPlayer = grid.coinsPerPlayer; // reuse your setting
        int needed = owners.Count * coinsPerPlayer;
        if (slots.Count < needed)
            Debug.LogWarning($"[CoinSpawnGridNet] Not enough slots: have {slots.Count}, need {needed}");

        // Optional: clear old children (server-side)
        if (grid.clearBeforeSpawn)
            for (int s = 0; s < slots.Count; s++)
                for (int i = slots[s].childCount - 1; i >= 0; i--)
                    Destroy(slots[s].GetChild(i).gameObject);

        int slotIdx = 0;
        foreach (var ownerNetId in owners)
        {
            for (int i = 0; i < coinsPerPlayer; i++)
            {
                if (slotIdx >= slots.Count) break;
                var slot = slots[slotIdx];
                SpawnOwnedCoinAtSlot_Server(ownerNetId, slot, slotIdx, registry);
                slotIdx++;
            }
        }
    }

    [Server]
    void SpawnOwnedCoinAtSlot_Server(uint ownerNetId, Transform slot, int slotIndex, ColorLockRegistry registry)
    {
        // Instantiate under slot so server renders it in the right canvas immediately
        var go = Instantiate(coinPrefab, slot, false);

        // Position/size for world-space UI
        if (go.transform is RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.sizeDelta = Vector2.zero;
        }

        // Initialize coin visuals (color) before spawn (purely cosmetic)
        var binding = go.GetComponent<CoinPlayerBinding>() ?? go.AddComponent<CoinPlayerBinding>();
        binding.ownerNetId = ownerNetId;

        if (registry.colorByOwner.TryGetValue(ownerNetId, out var color))
        {
            if (!binding.ui) binding.ui = go.GetComponent<CoinMakerUI>();
            if (binding.ui) binding.ui.SetPlayerColor(color);
            else binding.RefreshColor();
        }
        else binding.RefreshColor();

        // Tell the coin which slot to attach to on each client
        var attach = go.GetComponent<CoinSlotAttach>();
        if (!attach) attach = go.AddComponent<CoinSlotAttach>();
        attach.slotIndex = slotIndex;
        attach.slotsRootTag = slotsRootTag;

        // Give client authority to the owner so their input can drive NetworkTransform
        if (NetworkServer.spawned.TryGetValue(ownerNetId, out var ownerIdentity) &&
            ownerIdentity.connectionToClient != null)
        {
            NetworkServer.Spawn(go, ownerIdentity.connectionToClient); // owner gets authority
        }
        else
        {
            // Fallback: server-authoritative spawn if owner not found
            NetworkServer.Spawn(go);
        }
    }
}
