using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ColorReservationManager : NetworkBehaviour
{
    public static ColorReservationManager Instance;

    // swatchIndex -> playerNetId
    public readonly SyncDictionary<int, uint> lockedBy = new SyncDictionary<int, uint>();

    public event System.Action OnStateChanged;

    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        lockedBy.Clear();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Subscribe to dictionary changes (Mirror newer API)
        lockedBy.OnChange += OnDictChanged;

        // Kick one initial refresh for late-joiners
        OnStateChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        // Unsubscribe
        lockedBy.OnChange -= OnDictChanged;
    }

    // NOTE: Signature matches Mirror's SyncIDictionary OnChange delegate
    void OnDictChanged(SyncIDictionary<int, uint>.Operation op, int key, uint value)
    {
        OnStateChanged?.Invoke();
    }

    // ---------- Queries ----------
    [Server]
    public int GetIndexForPlayer(uint playerNetId)
    {
        foreach (var kv in lockedBy)
            if (kv.Value == playerNetId)
                return kv.Key;
        return -1;
    }

    public bool IsLocked(int swatchIndex) => lockedBy.ContainsKey(swatchIndex);

    // ---------- Mutations (Server only) ----------
    [Server]
    public bool TryLock(int swatchIndex, NetworkIdentity requester)
    {
        if (!requester) return false;

        // already taken?
        if (lockedBy.ContainsKey(swatchIndex))
            return false;

        // release any previous lock by this player
        int prev = GetIndexForPlayer(requester.netId);
        if (prev >= 0) lockedBy.Remove(prev);

        // claim
        lockedBy[swatchIndex] = requester.netId;
        return true;
    }

    [Server]
    public void ReleaseByPlayer(uint playerNetId)
    {
        int idx = GetIndexForPlayer(playerNetId);
        if (idx >= 0) lockedBy.Remove(idx);
    }
}
