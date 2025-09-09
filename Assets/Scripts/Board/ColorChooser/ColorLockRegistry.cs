using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ColorLockRegistry : NetworkBehaviour
{
    public static ColorLockRegistry Instance;

    // key: swatch index, value: owner player netId
    public readonly SyncDictionary<int, uint> lockedBy = new SyncDictionary<int, uint>();

    public delegate void RegistryChanged();
    public event RegistryChanged OnRegistryChanged;

    void Awake()
    {
        Instance = this;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // ✅ Newer Mirror API:
        lockedBy.OnChange += OnDictChanged;

        // Make sure late joiners get an immediate refresh
        OnRegistryChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        // Always unsubscribe to avoid leaks
        lockedBy.OnChange -= OnDictChanged;
        base.OnStopClient();
    }

    // Newer Mirror signature for SyncDictionary change notifications:
    void OnDictChanged(SyncDictionary<int, uint>.Operation op, int key, uint value)
    {
        OnRegistryChanged?.Invoke();
    }

    // --- Server API ---

    [Server]
    public bool TryConfirm(NetworkIdentity player, int newIndex)
    {
        if (newIndex < 0) return false;
        if (lockedBy.ContainsKey(newIndex)) return false; // already taken

        // ensure each player can only hold a single color
        int previous = FindIndexLockedBy(player.netId);
        if (previous >= 0) lockedBy.Remove(previous);

        lockedBy[newIndex] = player.netId;
        return true;
    }

    [Server]
    public void UnlockAllFor(NetworkIdentity player)
    {
        if (!player) return;
        var toRemove = new List<int>();
        foreach (var kv in lockedBy)
            if (kv.Value == player.netId) toRemove.Add(kv.Key);
        foreach (var idx in toRemove) lockedBy.Remove(idx);
    }

    [Server]
    public int FindIndexLockedBy(uint netId)
    {
        foreach (var kv in lockedBy)
            if (kv.Value == netId) return kv.Key;
        return -1;
    }
}
