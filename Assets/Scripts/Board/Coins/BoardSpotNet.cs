using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class BoardSpotsNet : NetworkBehaviour
{
    public static BoardSpotsNet Instance { get; private set; }

    [Header("Assign OR leave empty to auto-collect at runtime")]
    [SerializeField] List<ValidDropSpot> spots = new List<ValidDropSpot>();

    public class SpotDict : SyncDictionary<int, uint> { }
    public SpotDict occupancy = new SpotDict();

    public class CoinDict : SyncDictionary<uint, int> { }
    public CoinDict coinToIndex = new CoinDict();

    Dictionary<int, ValidDropSpot> _indexToSpot = new Dictionary<int, ValidDropSpot>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        CollectAndIndexSpotsIfNeeded();

        foreach (var key in _indexToSpot.Keys)
            if (!occupancy.ContainsKey(key)) occupancy[key] = 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        CollectAndIndexSpotsIfNeeded();

        foreach (var kv in occupancy)
            ApplyLocalSpot(kv.Key, kv.Value);
    }

    void CollectAndIndexSpotsIfNeeded()
    {
        if (spots == null || spots.Count == 0)
        {
            spots = FindObjectsByType<ValidDropSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            spots = spots.OrderBy(s => GetHierarchyPath(s.transform)).ToList();
        }

        _indexToSpot.Clear();
        int next = 0;
        foreach (var s in spots)
        {
            if (s == null) continue;
            if (s.spotIndex < 0) s.spotIndex = next;
            _indexToSpot[s.spotIndex] = s;
            next = Mathf.Max(next, s.spotIndex + 1);
        }
    }

    static string GetHierarchyPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack);
    }

    void ApplyLocalSpot(int index, uint coinNetId)
    {
        if (!_indexToSpot.TryGetValue(index, out var spot) || spot == null) return;

        if (coinNetId == 0)
        {
            spot.ForceClear();
            return;
        }

        if (NetworkClient.active && NetworkClient.spawned.TryGetValue(coinNetId, out var id))
        {
            spot.ForceOccupy(id.gameObject);
        }
        else
        {
            spot.ForceOccupy(null);
        }
    }

    public void RequestClaim(int spotIndex, NetworkIdentity coinIdentity, System.Action<bool, Vector3> callback)
    {
        if (!isClient || coinIdentity == null)
        {
            callback?.Invoke(false, Vector3.zero);
            return;
        }
        _pendingCallbacks[spotIndex] = callback;
        CmdRequestClaim(spotIndex, coinIdentity.netId);
    }

    readonly Dictionary<int, System.Action<bool, Vector3>> _pendingCallbacks = new();

    [Command(requiresAuthority = false)]
    void CmdRequestClaim(int spotIndex, uint coinNetId, NetworkConnectionToClient sender = null)
    {
        bool ok = false;
        Vector3 center = Vector3.zero;

        if (_indexToSpot.TryGetValue(spotIndex, out var spot))
        {
            uint cur = occupancy.ContainsKey(spotIndex) ? occupancy[spotIndex] : 0;

            if (cur == 0)
            {
                if (coinToIndex.TryGetValue(coinNetId, out var oldIdx))
                {
                    if (occupancy.ContainsKey(oldIdx)) occupancy[oldIdx] = 0;
                    coinToIndex.Remove(coinNetId);
                    RpcApplySpot(oldIdx, 0);
                }

                occupancy[spotIndex] = coinNetId;
                coinToIndex[coinNetId] = spotIndex;

                center = spot.GetCenterWorld();
                ok = true;

                RpcApplySpot(spotIndex, coinNetId);
            }
        }

        TargetClaimResult(sender, ok, spotIndex, center);
    }

    [TargetRpc]
    void TargetClaimResult(NetworkConnection target, bool ok, int spotIndex, Vector3 worldCenter)
    {
        if (_pendingCallbacks.TryGetValue(spotIndex, out var cb))
        {
            _pendingCallbacks.Remove(spotIndex);
            cb?.Invoke(ok, worldCenter);
        }
    }

    [ClientRpc]
    void RpcApplySpot(int index, uint coinNetId)
    {
        ApplyLocalSpot(index, coinNetId);
    }

    [Command(requiresAuthority = false)]
    public void CmdReleaseSpotByCoin(uint coinNetId)
    {
        if (coinToIndex.TryGetValue(coinNetId, out var idx))
        {
            coinToIndex.Remove(coinNetId);
            if (occupancy.ContainsKey(idx)) occupancy[idx] = 0;
            RpcApplySpot(idx, 0);
        }

        if (NetworkServer.spawned.TryGetValue(coinNetId, out var idObj))
        {
            var lockComp = idObj.GetComponent<CoinPlacedLock>();
            if (lockComp) lockComp.ServerSetLocked(false);
        }
    }
}
