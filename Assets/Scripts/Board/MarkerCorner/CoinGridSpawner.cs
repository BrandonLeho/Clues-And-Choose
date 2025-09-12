using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class CoinGridSpawner : NetworkBehaviour
{
    [Header("Setup")]
    [Tooltip("Prefab with NetworkIdentity, CoinMakerUI, DraggableCoin, CoinDragSync on the root")]
    public GameObject coinPrefab;

    [Tooltip("The GridLayoutGroup root (or any parent whose children are the slots)")]
    public Transform gridSlotsRoot;

    [Tooltip("Auto-discover slots as all direct children of gridSlotsRoot")]
    public bool autoDiscoverSlots = true;

    [Tooltip("Optionally clear any existing coins before spawning")]
    public bool clearExistingCoins = true;

    [Header("Timing")]
    [Tooltip("If true, server spawns coins in OnStartServer; clients only apply colors.")]
    public bool spawnOnStartServer = true;

    readonly List<Transform> _slots = new List<Transform>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (spawnOnStartServer)
            Server_SpawnAll();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        StartCoroutine(Client_WaitAndApplyColors());
        StartCoroutine(Client_HookRegistry());
    }

    void OnDisable()
    {
        if (!isClient) return;
        var reg = ColorLockRegistry.GetOrFind();
        if (reg) reg.OnRegistryChanged -= Client_RefreshAllColors;
    }

    [Server]
    public void Server_SpawnAll()
    {
        if (!coinPrefab)
        {
            Debug.LogWarning("[CoinGridSpawner] coinPrefab is not set.");
            return;
        }
        if (!gridSlotsRoot)
        {
            Debug.LogWarning("[CoinGridSpawner] gridSlotsRoot is not set.");
            return;
        }

        BuildSlotList();

        var reg = ColorLockRegistry.Instance;
        if (!reg)
        {
            Debug.LogWarning("[CoinGridSpawner] No ColorLockRegistry on server.");
            return;
        }

        var entries = reg.colorByOwner.ToArray();
        int playerCount = entries.Length;
        int need = Mathf.Min(_slots.Count, playerCount * 2);
        if (need == 0) return;

        if (clearExistingCoins)
        {
            for (int i = 0; i < need; i++)
            {
                var slot = _slots[i];
                for (int c = slot.childCount - 1; c >= 0; c--)
                {
                    var child = slot.GetChild(c).gameObject;
                    var ni = child.GetComponent<NetworkIdentity>();
                    if (ni && ni.isServer) NetworkServer.Destroy(child);
                    else Destroy(child);
                }
            }
        }

        for (int i = 0; i < need; i++)
        {
            int playerIndex = i / 2;
            var entry = entries[playerIndex];
            uint ownerNetId = entry.Key;
            var slot = _slots[i];

            if (!NetworkServer.spawned.TryGetValue(ownerNetId, out var ownerNI) || ownerNI == null)
            {
                Debug.LogWarning($"[CoinGridSpawner] Owner netId {ownerNetId} not found in spawned table.");
                continue;
            }

            var coin = Instantiate(coinPrefab, slot, false);

            var ui = coin.GetComponent<CoinMakerUI>();
            if (ui) ui.SetPlayerColor(entry.Value);

            var dr = coin.GetComponent<DraggableCoin>();
            if (dr) dr.ownerNetId = ownerNetId;

            var sync = coin.GetComponent<CoinDragSync>();
            if (sync) sync.ownerNetId = ownerNetId;

            var rt = coin.transform as RectTransform;
            if (rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            NetworkServer.Spawn(coin, ownerNI.connectionToClient);
        }
    }

    IEnumerator Client_WaitAndApplyColors()
    {
        ColorLockRegistry reg = null;
        float timeout = 3f;
        while (timeout > 0f && (reg = ColorLockRegistry.GetOrFind()) == null)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (reg == null) yield break;

        Client_RefreshAllColors();
    }

    IEnumerator Client_HookRegistry()
    {
        ColorLockRegistry reg = null;
        while ((reg = ColorLockRegistry.GetOrFind()) == null) yield return null;
        reg.OnRegistryChanged += Client_RefreshAllColors;
        yield break;
    }

    void Client_RefreshAllColors()
    {
        var reg = ColorLockRegistry.GetOrFind();
        if (!reg) return;

        BuildSlotList();

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            for (int c = 0; c < slot.childCount; c++)
            {
                var go = slot.GetChild(c).gameObject;

                uint owner = 0;
                var dr = go.GetComponent<DraggableCoin>();
                if (dr) owner = dr.ownerNetId;

                if (owner == 0)
                {
                    var bind = go.GetComponent<CoinPlayerBinding>();
                    if (bind) owner = bind.ownerNetId;
                }

                if (owner == 0) continue;

                if (reg.colorByOwner.TryGetValue(owner, out var color))
                {
                    var ui = go.GetComponent<CoinMakerUI>();
                    if (ui) ui.SetPlayerColor(color);
                }
            }
        }
    }

    void BuildSlotList()
    {
        _slots.Clear();
        if (!gridSlotsRoot) return;

        if (autoDiscoverSlots)
        {
            for (int i = 0; i < gridSlotsRoot.childCount; i++)
                _slots.Add(gridSlotsRoot.GetChild(i));
        }
        else
        {
            for (int i = 0; i < gridSlotsRoot.childCount; i++)
                _slots.Add(gridSlotsRoot.GetChild(i));
        }
    }
}
