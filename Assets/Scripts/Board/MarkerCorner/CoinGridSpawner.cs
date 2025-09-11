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

    void OnEnable()
    {
        if (spawnOnEnable) SpawnNow();
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

        if (_slots.Count == 0)
        {
            Debug.LogWarning("[CoinGridSpawner] No slots found under gridSlotsRoot.");
            return;
        }

        var reg = ColorLockRegistry.Instance;
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

        for (int i = 0; i < need; i++)
        {
            int playerIndex = i / 2;
            var entry = entries[playerIndex];
            var slot = _slots[i];

            var coin = Instantiate(coinPrefab, slot, false);
            var ui = coin.GetComponent<CoinMakerUI>();
            if (!ui)
            {
                Debug.LogWarning("[CoinGridSpawner] coinPrefab missing CoinMakerUI component.");
            }
            else
            {
                ui.SetPlayerColor(entry.Value);
                ui.FlashOnPlace();
            }

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
