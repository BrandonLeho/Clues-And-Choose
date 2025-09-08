using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ColorReservationManager : NetworkBehaviour
{
    public static ColorReservationManager Instance;

    // swatchIndex -> ownerNetId
    public class SyncDictIntUInt : SyncDictionary<int, uint> { }
    public SyncDictIntUInt reservations = new SyncDictIntUInt();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        reservations.Clear();
    }

    // ---------- SERVER API ----------
    [Server]
    public bool TryReserve(uint ownerNetId, int swatchIndex, out int previousIndex)
    {
        previousIndex = -1;

        // Already owned by same player? (idempotent)
        if (reservations.TryGetValue(swatchIndex, out var existingOwner))
            return existingOwner == ownerNetId;

        // Free previous reservation (if any) for this owner
        foreach (KeyValuePair<int, uint> kv in reservations)
        {
            if (kv.Value == ownerNetId)
            {
                previousIndex = kv.Key;
                break;
            }
        }
        if (previousIndex != -1) reservations.Remove(previousIndex);

        // If still free, take it
        if (!reservations.ContainsKey(swatchIndex))
        {
            reservations[swatchIndex] = ownerNetId;
            return true;
        }
        return false; // lost a race
    }

    [Server]
    public void ReleaseByOwner(uint ownerNetId)
    {
        int found = -1;
        foreach (KeyValuePair<int, uint> kv in reservations)
            if (kv.Value == ownerNetId) { found = kv.Key; break; }

        if (found != -1) reservations.Remove(found);
    }
}
