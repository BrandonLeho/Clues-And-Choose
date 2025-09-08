using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ColorReservationManager : NetworkBehaviour
{
    public static ColorReservationManager Instance;

    // index -> ownerNetId
    public class SyncDictIntUInt : SyncDictionary<int, uint> { }
    public SyncDictIntUInt reservations = new SyncDictIntUInt();

    // Bumped on any change, so clients can cheaply detect updates
    [SyncVar] public int version;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // --- Server API ---
    [Server]
    public bool TryReserve(uint ownerNetId, int swatchIndex, out int previousIndex)
    {
        previousIndex = -1;

        // If already owned by this player, accept (no change)
        uint existing;
        if (reservations.TryGetValue(swatchIndex, out existing))
            return existing == ownerNetId;

        // Free player's previous reservation (if any)
        int toRemove = -1;
        foreach (KeyValuePair<int, uint> kv in reservations)
        {
            if (kv.Value == ownerNetId)
            {
                toRemove = kv.Key;
                break;
            }
        }
        if (toRemove != -1)
        {
            previousIndex = toRemove;
            reservations.Remove(toRemove);
        }

        // Only reserve if free
        if (!reservations.ContainsKey(swatchIndex))
        {
            reservations[swatchIndex] = ownerNetId;
            version++; // notify clients
            return true;
        }
        return false;
    }

    [Server]
    public void ReleaseByOwner(uint ownerNetId)
    {
        int toRemove = -1;
        foreach (KeyValuePair<int, uint> kv in reservations)
        {
            if (kv.Value == ownerNetId)
            {
                toRemove = kv.Key;
                break;
            }
        }
        if (toRemove != -1)
        {
            reservations.Remove(toRemove);
            version++; // notify clients
        }
    }

    public override void OnStartServer()
    {
        // Optional: clear on server start
        reservations.Clear();
        version = 0;
    }
}
