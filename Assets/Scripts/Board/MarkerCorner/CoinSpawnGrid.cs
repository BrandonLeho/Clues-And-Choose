using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CoinSpawnGrid : MonoBehaviour
{
    [Header("Slots & Prefab")]
    [Tooltip("Parent with exactly 20 slot children (typically an empty GameObject under the GridLayoutGroup).")]
    [SerializeField] Transform slotsRoot;

    [Tooltip("UI coin prefab that contains CoinMakerUI + CoinPlayerBinding.")]
    [SerializeField] GameObject coinPrefab;

    [Header("Spawn Rules")]
    [Min(1)] public int coinsPerPlayer = 2;
    [Tooltip("Clear any previously spawned coins before new spawn.")]
    public bool clearBeforeSpawn = true;

    [Header("Auto-Hook")]
    [Tooltip("Find ColorChoosingPhaseController and run SpawnNow when onPhaseEnded fires.")]
    public bool spawnOnChoosingPhaseEnd = true;

    // internal cache
    readonly List<Transform> _slots = new List<Transform>();
    bool _hooked;

    void Awake()
    {
        if (!slotsRoot)
        {
            // If not set, try the GridLayoutGroup's transform
            var grid = GetComponentInChildren<GridLayoutGroup>(true);
            if (grid) slotsRoot = grid.transform;
        }

        _slots.Clear();
        if (slotsRoot)
        {
            for (int i = 0; i < slotsRoot.childCount; i++)
                _slots.Add(slotsRoot.GetChild(i));
        }
    }

    void OnEnable()
    {
        if (!spawnOnChoosingPhaseEnd) return;

        // Find the phase controller and add a one-time listener to its onPhaseEnded event
        var phase = FindFirstObjectByType<ColorChoosingPhaseController>();
        if (phase && !_hooked)
        {
            _hooked = true;
            phase.onPhaseEnded.AddListener(SpawnNow);
        }
    }

    [ContextMenu("Spawn Now")]
    public void SpawnNow()
    {
        var registry = ColorLockRegistry.GetOrFind();
        if (!registry)
        {
            Debug.LogWarning("[CoinSpawnGrid] No ColorLockRegistry found, aborting spawn.");
            return;
        }

        // Who is playing? Use the owners present in colorByOwner (each has a chosen color).
        // Sort by the swatch index for a stable order (top-left to bottom-right feel).
        var owners = registry.colorByOwner.Keys.ToList();
        owners.Sort((a, b) =>
        {
            int ia = registry.indexByOwner.TryGetValue(a, out var ai) ? ai : int.MaxValue;
            int ib = registry.indexByOwner.TryGetValue(b, out var bi) ? bi : int.MaxValue;
            return ia.CompareTo(ib);
        });

        int needed = owners.Count * coinsPerPlayer;
        if (_slots.Count < needed)
        {
            Debug.LogWarning($"[CoinSpawnGrid] Not enough slots: have {_slots.Count}, need {needed}.");
            // We’ll still fill as many as possible.
        }

        if (clearBeforeSpawn) ClearAllCoinsInSlots();

        int slotIdx = 0;
        foreach (var owner in owners)
        {
            for (int i = 0; i < coinsPerPlayer; i++)
            {
                if (slotIdx >= _slots.Count) break;
                var slot = _slots[slotIdx++];
                SpawnCoinForOwnerInSlot(owner, slot, registry);
            }
        }
    }

    void ClearAllCoinsInSlots()
    {
        foreach (var s in _slots)
        {
            if (!s) continue;
            for (int i = s.childCount - 1; i >= 0; i--)
                DestroyImmediate(s.GetChild(i).gameObject);
        }
    }

    void SpawnCoinForOwnerInSlot(uint ownerNetId, Transform slot, ColorLockRegistry registry)
    {
        if (!coinPrefab || !slot) return;

        // Instantiate as child of the slot, and normalize RectTransform so it doesn't appear huge.
        var go = Instantiate(coinPrefab, slot, false);

        // Ensure UI transform sane defaults
        var rt = go.transform as RectTransform;
        if (rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.sizeDelta = Vector2.zero; // let layout/auto-size handle it
        }

        // Bind owner → color via your CoinPlayerBinding
        var binding = go.GetComponent<CoinPlayerBinding>();
        if (!binding) binding = go.AddComponent<CoinPlayerBinding>();
        binding.ownerNetId = ownerNetId;

        // If color already present, apply it immediately; otherwise the binding can refresh later.
        if (registry.colorByOwner.TryGetValue(ownerNetId, out var c))
        {
            if (!binding.ui) binding.ui = go.GetComponent<CoinMakerUI>();
            if (binding.ui) binding.ui.SetPlayerColor(c);
        }
        else
        {
            // Fallback: ask binding to refresh when the registry updates
            binding.RefreshColor();
        }
    }
}
