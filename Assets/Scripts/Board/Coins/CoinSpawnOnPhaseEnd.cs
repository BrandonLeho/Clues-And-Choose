using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class CoinSpawnOnPhaseEnd : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] CoinSpawner spawner;
    [SerializeField] ColorChoosingPhaseController phase;
    [Header("Timing")]
    [Tooltip("Extra delay after phase end before spawning (lets the game group fade/enable).")]
    [SerializeField] float postPhaseDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] bool logInfo = true;

    void Reset()
    {
        if (!phase) phase = FindFirstObjectByType<ColorChoosingPhaseController>(FindObjectsInactive.Include);
        if (!spawner) spawner = FindFirstObjectByType<CoinSpawner>(FindObjectsInactive.Include);
    }

    public void TriggerSpawnFromPhaseEnd()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Co_SpawnAfterDelay());
    }

    public void SpawnNow()
    {
        SpawnFromRegistry();
    }

    System.Collections.IEnumerator Co_SpawnAfterDelay()
    {
        if (postPhaseDelay > 0f) yield return new WaitForSecondsRealtime(postPhaseDelay);
        SpawnFromRegistry();
    }

    void SpawnFromRegistry()
    {
        if (!spawner)
        {
            Debug.LogError("[CoinSpawnOnPhaseEnd] Spawner not assigned.");
            return;
        }

        var registry = ColorLockRegistry.GetOrFind();
        if (!registry)
        {
            Debug.LogError("[CoinSpawnOnPhaseEnd] ColorLockRegistry not found.");
            return;
        }

        var colors = new List<Color>();
        int maxIndex = -1;
        foreach (var kv in registry.lockedBy) if (kv.Key > maxIndex) maxIndex = kv.Key;

        for (int idx = 0; idx <= maxIndex; idx++)
        {
            if (registry.lockedBy.TryGetValue(idx, out uint ownerId))
            {
                if (registry.colorByOwner.TryGetValue(ownerId, out Color32 c32))
                    colors.Add((Color)c32);
            }
        }

        int playerCount = colors.Count;
        if (playerCount <= 0)
        {
            if (logInfo) Debug.Log("[CoinSpawnOnPhaseEnd] No locked colors; skipping spawn.");
            return;
        }

        if (logInfo) Debug.Log($"[CoinSpawnOnPhaseEnd] Spawning coins for {playerCount} players (2 each).");
        spawner.CollectSlots();
        spawner.SpawnForPlayers(playerCount, colors);
    }
}
