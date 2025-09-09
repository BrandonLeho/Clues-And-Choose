using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ColorLockRegistry : NetworkBehaviour
{
    public static ColorLockRegistry Instance;

    // index -> ownerNetId
    public readonly SyncDictionary<int, uint> lockedBy = new SyncDictionary<int, uint>();

    public delegate void RegistryChanged();
    public event RegistryChanged OnRegistryChanged;

    void Awake() => Instance = this;

    public override void OnStartClient()
    {
        base.OnStartClient();
        lockedBy.OnChange += OnDictChanged;
        // Optional: remove this first invoke if you want to wait for initial sync
        OnRegistryChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        lockedBy.OnChange -= OnDictChanged;
        base.OnStopClient();
    }

    void OnDictChanged(SyncDictionary<int, uint>.Operation op, int key, uint value)
        => OnRegistryChanged?.Invoke();

    // -------- Server API (mutations) --------
    [Server]
    public bool TryConfirm(NetworkIdentity player, int newIndex)
    {
        if (newIndex < 0) return false;
        if (lockedBy.ContainsKey(newIndex)) return false;

        // one color per player
        int previous = FindIndexLockedByServer(player.netId);
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
    public int FindIndexLockedByServer(uint netId)
    {
        foreach (var kv in lockedBy)
            if (kv.Value == netId) return kv.Key;
        return -1;
    }

    // -------- Client/any side READ-ONLY helper --------
    // NOTE: no [Server] attribute here!
    public int FindIndexLockedByLocal(uint netId)
    {
        foreach (var kv in lockedBy)
            if (kv.Value == netId) return kv.Key;
        return -1;
    }

    // Optional hygiene: clear when a new round/scene starts on server
    public override void OnStartServer()
    {
        base.OnStartServer();
        lockedBy.Clear();
    }
}
