using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ColorLockRegistry : NetworkBehaviour
{
    public static ColorLockRegistry Instance;

    public readonly SyncDictionary<int, uint> lockedBy = new SyncDictionary<int, uint>();
    public readonly SyncDictionary<int, string> labelByIndex = new SyncDictionary<int, string>();

    public readonly SyncDictionary<uint, int> indexByOwner = new SyncDictionary<uint, int>();
    public readonly SyncDictionary<uint, Color32> colorByOwner = new SyncDictionary<uint, Color32>();

    public delegate void RegistryChanged();
    public event RegistryChanged OnRegistryChanged;

    public static ColorLockRegistry GetOrFind()
    {
        if (Instance) return Instance;
        Instance = FindFirstObjectByType<ColorLockRegistry>(FindObjectsInactive.Include);
        return Instance;
    }


    void Awake() => Instance = this;

    public override void OnStartClient()
    {
        base.OnStartClient();
        lockedBy.OnChange += OnDictChanged;
        labelByIndex.OnChange += (_, __, ___) => OnRegistryChanged?.Invoke();
        indexByOwner.OnChange += (_, __, ___) => OnRegistryChanged?.Invoke();
        colorByOwner.OnChange += (_, __, ___) => OnRegistryChanged?.Invoke();
        OnRegistryChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        lockedBy.OnChange -= OnDictChanged;
        labelByIndex.OnChange -= (_, __, ___) => OnRegistryChanged?.Invoke();
        indexByOwner.OnChange -= (_, __, ___) => OnRegistryChanged?.Invoke();
        colorByOwner.OnChange -= (_, __, ___) => OnRegistryChanged?.Invoke();
        base.OnStopClient();
    }

    void OnDictChanged(SyncDictionary<int, uint>.Operation op, int key, uint value)
        => OnRegistryChanged?.Invoke();

    [Server]
    public bool TryConfirm(NetworkIdentity player, int newIndex, Color32 color)
    {
        if (newIndex < 0) return false;
        if (lockedBy.ContainsKey(newIndex)) return false;

        int previous = FindIndexLockedByServer(player.netId);
        if (previous >= 0)
        {
            lockedBy.Remove(previous);
            labelByIndex.Remove(previous);
            indexByOwner.Remove(player.netId);
            colorByOwner.Remove(player.netId);
        }

        string owner = player.GetComponent<PlayerNameSync>()?.DisplayName;
        if (string.IsNullOrWhiteSpace(owner)) owner = $"Player {player.netId}";

        lockedBy[newIndex] = player.netId;
        labelByIndex[newIndex] = owner;

        indexByOwner[player.netId] = newIndex;
        colorByOwner[player.netId] = color;
        return true;
    }

    [Server]
    public void UnlockAllFor(NetworkIdentity player)
    {
        if (!player) return;
        var toRemove = new List<int>();
        foreach (var kv in lockedBy)
            if (kv.Value == player.netId) toRemove.Add(kv.Key);

        foreach (var idx in toRemove)
        {
            lockedBy.Remove(idx);
            labelByIndex.Remove(idx);
        }
        indexByOwner.Remove(player.netId);
        colorByOwner.Remove(player.netId);
    }

    [Server]
    public int FindIndexLockedByServer(uint netId)
    {
        foreach (var kv in lockedBy)
            if (kv.Value == netId) return kv.Key;
        return -1;
    }

    public int FindIndexLockedByLocal(uint netId)
    {
        foreach (var kv in lockedBy)
            if (kv.Value == netId) return kv.Key;
        return -1;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        lockedBy.Clear();
    }
}
