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

        var reg = ColorLockRegistry.GetOrFind();
        if (!reg)
        {
            Debug.LogWarning("[CoinGridSpawner] No ColorLockRegistry in scene.");
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
                    Destroy(slot.GetChild(c).gameObject);
            }
        }

        _spawned.Clear();

        for (int i = 0; i < need; i++)
        {
            int playerIndex = i / 2;
            var entry = entries[playerIndex];
            var slot = _slots[i];

            var coin = Instantiate(coinPrefab, slot, false);

            if (NetworkServer.active)
            {
                Debug.Log("aisudghosaihgioasdhgklsadg");
                var ni = coin.GetComponent<NetworkIdentity>();
                if (!ni) ni = coin.AddComponent<NetworkIdentity>();
                if (NetworkServer.spawned.TryGetValue(entry.Key, out var ownerNI))
                {
                    NetworkServer.Spawn(coin, ownerNI.connectionToClient);
                }
                else
                {
                    NetworkServer.Spawn(coin);
                }
            }


            var ui = coin.GetComponent<CoinMakerUI>();
            if (ui) ui.SetPlayerColor(entry.Value);

            var bind = coin.GetComponent<CoinPlayerBinding>() ?? coin.AddComponent<CoinPlayerBinding>();
            bind.ownerNetId = entry.Key;
            bind.ui = ui;
            bind.RefreshColor();
            _spawned.Add(bind);

            var dr = coin.GetComponent<DraggableCoin>();
            if (dr) dr.ownerNetId = entry.Key;

            var sync = coin.GetComponent<CoinDragSync>();
            if (sync) sync.ownerNetId = entry.Key;

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
