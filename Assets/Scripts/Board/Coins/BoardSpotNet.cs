using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class BoardSpotsNet : NetworkBehaviour
{
    public static BoardSpotsNet Instance { get; private set; }

    [Header("Assign OR leave empty to auto-collect at runtime")]
    [SerializeField] List<ValidDropSpot> spots = new List<ValidDropSpot>();

    // index -> coin netId (0 = empty)
    public class SpotDict : SyncDictionary<int, uint> { }
    public SpotDict occupancy = new SpotDict();

    // coin netId -> index
    public class CoinDict : SyncDictionary<uint, int> { }
    public CoinDict coinToIndex = new CoinDict();

    // fast local map
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

        Debug.Log($"[BOARD] OnStartServer: spots={_indexToSpot.Count} initialized occupancy keys={occupancy.Count}");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        CollectAndIndexSpotsIfNeeded();

        foreach (var kv in occupancy)
            ApplyLocalSpot(kv.Key, kv.Value);

        var bni = GetComponent<NetworkIdentity>();
        Debug.Log($"[BOARD] OnStartClient: spots={_indexToSpot.Count} occKeys={occupancy.Count} hasNI={(bni != null)} isServer={NetworkServer.active} isClient={NetworkClient.active}");
    }

    void CollectAndIndexSpotsIfNeeded()
    {
        if (spots == null || spots.Count == 0)
        {
            spots = FindObjectsOfType<ValidDropSpot>(true).ToList();
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

        // Sanity
        var dup = _indexToSpot.GroupBy(kv => kv.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dup.Count > 0) Debug.LogError("[BOARD] Duplicate indices: " + string.Join(",", dup));
        int noIndex = spots.Count(s => s.spotIndex < 0);
        if (noIndex > 0) Debug.LogWarning($"[BOARD] {noIndex} spots still unindexed (<0).");
    }

    static string GetHierarchyPath(Transform t)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack);
    }

    void ApplyLocalSpot(int index, uint coinNetId)
    {
        Debug.Log($"[BOARD] ApplyLocalSpot idx={index} coin={(coinNetId)}");

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
            // Coin not spawned yet here, still mark occupied
            spot.ForceOccupy(null);
        }
    }

    // ===================== Client API =====================

    public void RequestClaim(int spotIndex, NetworkIdentity coinIdentity, System.Action<bool, Vector3> callback)
    {
        Debug.Log($"[BOARD] Client RequestClaim spot={spotIndex} coin={(coinIdentity ? coinIdentity.netId : 0)}");

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
        Debug.Log($"[BOARD] Server CmdRequestClaim spot={spotIndex} coin={coinNetId} sender={sender?.connectionId}");

        bool ok = false;
        Vector3 center = Vector3.zero;

        if (_indexToSpot.TryGetValue(spotIndex, out var spot))
        {
            uint cur = occupancy.ContainsKey(spotIndex) ? occupancy[spotIndex] : 0;
            Debug.Log($"[BOARD] spot={spotIndex} current={cur} hasSpotObj={_indexToSpot.ContainsKey(spotIndex)}");

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

                Debug.Log($"[BOARD] ACCEPT spot={spotIndex} <- coin={coinNetId}  center={center}");
                RpcApplySpot(spotIndex, coinNetId);
            }
        }

        if (!ok) Debug.LogWarning($"[BOARD] REJECT spot={spotIndex} coin={coinNetId} (occupied or missing spot)");

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
        Debug.Log($"[BOARD] TargetClaimResult -> conn {target} ok={ok} spot={spotIndex} center={worldCenter}");
    }

    [ClientRpc]
    void RpcApplySpot(int index, uint coinNetId)
    {
        Debug.Log($"[BOARD] RpcApplySpot index={index} coinNetId={coinNetId}");
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
            Debug.Log($"[BOARD] Release by coin={coinNetId} cleared idx={idx}");
        }
    }
}
