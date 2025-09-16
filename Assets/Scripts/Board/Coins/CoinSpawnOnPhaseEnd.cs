using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class CoinSpawnOnPhaseEndNetwork : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] CoinNetworkSpawner networkSpawner;
    [SerializeField] ColorChoosingPhaseController phase;

    [Header("Timing")]
    [Tooltip("Extra delay after phase end before spawning (lets the game group fade/enable).")]
    [SerializeField] float postPhaseDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] bool logInfo = true;

    void Reset()
    {
        if (!phase) phase = FindFirstObjectByType<ColorChoosingPhaseController>(FindObjectsInactive.Include);
        if (!networkSpawner) networkSpawner = FindFirstObjectByType<CoinNetworkSpawner>(FindObjectsInactive.Include);
    }

    public void TriggerSpawnFromPhaseEnd()
    {
        if (!isActiveAndEnabled) return;
        if (postPhaseDelay > 0f) StartCoroutine(Co_Delayed());
        else RequestServerSpawn();
    }

    System.Collections.IEnumerator Co_Delayed()
    {
        yield return new WaitForSecondsRealtime(postPhaseDelay);
        RequestServerSpawn();
    }

    void RequestServerSpawn()
    {
        CmdRequestServerSpawn();
    }

    [Command(requiresAuthority = false)]
    void CmdRequestServerSpawn(NetworkConnectionToClient sender = null)
    {
        try
        {
            var reg = ColorLockRegistry.GetOrFind();
            if (!reg)
            {
                Debug.LogWarning("[CoinSpawnOnPhaseEndNetwork] No ColorLockRegistry on server.");
                return;
            }

            if (logInfo) DumpRegistryServer(reg);

            if (!networkSpawner)
            {
                networkSpawner = FindFirstObjectByType<CoinNetworkSpawner>(FindObjectsInactive.Include);
                if (!networkSpawner)
                {
                    Debug.LogError("[CoinSpawnOnPhaseEndNetwork] CoinNetworkSpawner not found on server.");
                    return;
                }
            }

            networkSpawner.TrySpawnOnce();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CoinSpawnOnPhaseEndNetwork] CmdRequestServerSpawn exception: {ex}");
        }
    }

    [Server]
    void DumpRegistryServer(ColorLockRegistry reg)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Registry (SERVER) ===");

        int players = NetworkServer.spawned.Values.Count(id => id && id.GetComponent<PlayerNameSync>());
        sb.AppendLine($"Players (with PlayerNameSync): {players}");

        if (reg.lockedBy.Count == 0) sb.AppendLine("lockedBy: <empty>");
        else foreach (var kv in reg.lockedBy) sb.AppendLine($"lockedBy: index {kv.Key} -> ownerNetId {kv.Value}");

        if (reg.colorByOwner.Count == 0) sb.AppendLine("colorByOwner: <empty>");
        else foreach (var kv in reg.colorByOwner) sb.AppendLine($"colorByOwner: owner {kv.Key} -> color {kv.Value}");

        if (reg.indexByOwner.Count == 0) sb.AppendLine("indexByOwner: <empty>");
        else foreach (var kv in reg.indexByOwner) sb.AppendLine($"indexByOwner: owner {kv.Key} -> index {kv.Value}");

        foreach (var kv in reg.lockedBy)
        {
            uint owner = kv.Value;
            bool hasColor = reg.colorByOwner.ContainsKey(owner);
            if (!hasColor) sb.AppendLine($"WARNING: owner {owner} present in lockedBy but missing in colorByOwner.");
        }

        Debug.Log(sb.ToString());
    }
}
