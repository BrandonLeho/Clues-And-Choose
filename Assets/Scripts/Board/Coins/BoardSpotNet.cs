using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class BoardSpotsNet : NetworkBehaviour
{
    public static BoardSpotsNet Instance { get; private set; }

    public readonly SyncDictionary<int, uint> occupancy = new SyncDictionary<int, uint>();

    static int _nextNonce = 1;
    static readonly Dictionary<int, System.Action<bool, Vector3>> _pending = new();

    void Awake() => Instance = this;

    public override void OnStartClient()
    {
        base.OnStartClient();

        occupancy.OnChange += OnOccChanged;

        foreach (var kv in occupancy)
            ApplyToLocal(kv.Key, kv.Value);
    }

    void OnOccChanged(SyncIDictionary<int, uint>.Operation op, int spotIndex, uint value)
    {
        switch (op)
        {
            case SyncIDictionary<int, uint>.Operation.OP_ADD:
            case SyncIDictionary<int, uint>.Operation.OP_SET:
                ApplyToLocal(spotIndex, value);
                break;

            case SyncIDictionary<int, uint>.Operation.OP_REMOVE:
                ApplyToLocal(spotIndex, 0);
                break;

            case SyncIDictionary<int, uint>.Operation.OP_CLEAR:
                var count = BoardSpotsRegistry.Instance?.Count ?? 0;
                for (int i = 0; i < count; i++) ApplyToLocal(i, 0);
                break;
        }
    }

    void ApplyToLocal(int spotIndex, uint coinNetId)
    {
        var spot = BoardSpotsRegistry.Instance?.Get(spotIndex);
        if (!spot) return;

        GameObject coinGO = null;

        if (coinNetId != 0 && NetworkClient.spawned.TryGetValue(coinNetId, out var ni))
            coinGO = ni.gameObject;

        spot.SetOccupantLocal(coinGO);
    }

    public static void RequestClaim(int spotIndex, NetworkIdentity coinNI, System.Action<bool, Vector3> cb)
    {
        var inst = Instance;

        if (!inst)
        {
            var spot = BoardSpotsRegistry.Instance?.Get(spotIndex);
            bool ok = (spot && spot.enabledForPlacement && !spot.isOccupied);
            if (ok) spot.SetOccupantLocal(coinNI ? coinNI.gameObject : null);
            cb?.Invoke(ok, ok ? spot.GetCenterWorld() : Vector3.zero);
            return;
        }

        int nonce = _nextNonce++;
        if (cb != null) _pending[nonce] = cb;
        inst.CmdRequestClaim(spotIndex, coinNI ? coinNI.netId : 0, nonce);
    }

    public static void RequestRelease(int spotIndex, NetworkIdentity coinNI)
    {
        Instance?.CmdReleaseIfOccupant(spotIndex, coinNI ? coinNI.netId : 0);
    }

    [Command(requiresAuthority = false)]
    void CmdRequestClaim(int spotIndex, uint coinNetId, int nonce, NetworkConnectionToClient sender = null)
    {
        if (!NetworkServer.spawned.TryGetValue(coinNetId, out var coinNI) ||
            coinNI.connectionToClient != sender)
        {
            TargetClaimResult(sender, nonce, false, Vector3.zero);
            return;
        }

        if (occupancy.ContainsKey(spotIndex))
        {
            TargetClaimResult(sender, nonce, false, Vector3.zero);
            return;
        }

        occupancy[spotIndex] = coinNetId;

        var spot = BoardSpotsRegistry.Instance?.Get(spotIndex);
        var snap = spot ? spot.GetCenterWorld() : coinNI.transform.position;

        TargetClaimResult(sender, nonce, true, snap);
    }

    [Command(requiresAuthority = false)]
    void CmdReleaseIfOccupant(int spotIndex, uint coinNetId, NetworkConnectionToClient sender = null)
    {
        if (occupancy.TryGetValue(spotIndex, out var cur) && cur == coinNetId)
        {
            if (NetworkServer.spawned.TryGetValue(coinNetId, out var coinNI) &&
                coinNI.connectionToClient == sender)
            {
                occupancy.Remove(spotIndex);
            }
        }
    }

    [TargetRpc]
    void TargetClaimResult(NetworkConnectionToClient target, int nonce, bool ok, Vector3 snapWorldPos)
    {
        if (_pending.Remove(nonce, out var cb))
            cb?.Invoke(ok, snapWorldPos);
    }
}
