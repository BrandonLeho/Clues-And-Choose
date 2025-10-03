using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mirror;
using UnityEngine;

public class CoinPlacementTurnManager : NetworkBehaviour
{
    public static CoinPlacementTurnManager Instance;

    [Header("Debug")]
    [SerializeField] bool debugLogs = true;

    [SyncVar(hook = nameof(OnPlacerChangedHook))]
    public uint currentPlacerNetId;

    public static System.Action<uint> OnPlacerChangedClient;

    List<uint> _order = new();
    int _idx = -1;

    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        base.OnStartServer();
        RoundManager.OnServerRosterChanged += RebuildOrderAndMaybeReset;
        RoundManager.OnServerClueGiverChanged += _ => RebuildOrderAndMaybeReset();
        RebuildOrderAndMaybeReset();
    }

    public override void OnStopServer()
    {
        RoundManager.OnServerRosterChanged -= RebuildOrderAndMaybeReset;
        RoundManager.OnServerClueGiverChanged -= _ => RebuildOrderAndMaybeReset();
        base.OnStopServer();
    }

    [Server]
    void RebuildOrderAndMaybeReset()
    {
        if (!RoundManager.Instance) return;

        var roster = RoundManager.Instance.ServerGetRosterSnapshot();
        uint clue = RoundManager.Instance.ServerGetClueGiverNetIdUnsafe();

        var newOrder = roster.Where(id => id != clue).ToList();

        int newIdx = (currentPlacerNetId != 0) ? newOrder.IndexOf(currentPlacerNetId) : -1;

        _order = newOrder;
        if (_order.Count == 0) { _idx = -1; SetPlacer(0); return; }

        _idx = (newIdx >= 0) ? newIdx : 0;
        SetPlacer(_order[_idx]);
        LogOrder("rebuild");
    }

    [Server]
    public void ServerBeginCycleAtFirst()
    {
        if (debugLogs) Debug.Log("[Turn] Begin cycle at first");

        RebuildOrderAndMaybeReset();
        if (_order.Count > 0) { _idx = 0; SetPlacer(_order[_idx]); }

        LogOrder("begin cycle");
        LogPlacerChanged(currentPlacerNetId, "by begin");
    }

    [Server]
    public void ServerAdvanceToNext()
    {
        if (debugLogs) Debug.Log("[Turn] Advance to next");

        if (_order.Count == 0) { SetPlacer(0); return; }
        int cur = _order.IndexOf(currentPlacerNetId);
        _idx = (cur >= 0 ? cur : -1) + 1;
        if (_idx >= _order.Count) _idx = 0;
        SetPlacer(_order[_idx]);

        LogOrder("advance");
        LogPlacerChanged(currentPlacerNetId, "by advance");
    }

    [Server]
    public bool ServerCanPlayerPlace(uint playerNetId)
    {
        bool can = _order.Count > 0 && playerNetId == currentPlacerNetId;
        if (debugLogs)
        {
            Debug.Log($"[Turn] CanPlace? requester={FmtPlayer(playerNetId)} " +
                      $"current={FmtPlayer(currentPlacerNetId)} => {(can ? "YES" : "NO")}");
        }
        return can;
    }

    void SetPlacer(uint id) => currentPlacerNetId = id;

    void OnPlacerChangedHook(uint _, uint newId)
    {
        LogPlacerChanged(newId, "↔ SyncVar hook (client)");
        OnPlacerChangedClient?.Invoke(newId);
    }

    public static bool IsLocalPlayersTurn()
    {
        var me = NetworkClient.connection?.identity;
        return me && Instance && Instance.currentPlacerNetId != 0 && me.netId == Instance.currentPlacerNetId;
    }

    static string FmtPlayer(uint netId)
    {
        if (netId == 0) return "(none)";
        string goName = null;

        if (NetworkServer.active && NetworkServer.spawned.TryGetValue(netId, out var srvId))
            goName = srvId.gameObject.name;
        else if (NetworkClient.active && NetworkClient.spawned.TryGetValue(netId, out var cliId))
            goName = cliId.gameObject.name;

        return goName != null ? $"{goName} [netId={netId}]" : $"[netId={netId}]";
    }

    void LogOrder(string reason)
    {
        if (!debugLogs) return;
        var sb = new StringBuilder();
        sb.Append("[Turn] Order (non–clue givers) after ").Append(reason).Append(": ");
        if (_order.Count == 0) sb.Append("(empty)");
        else
        {
            for (int i = 0; i < _order.Count; i++)
            {
                if (i > 0) sb.Append(" → ");
                sb.Append(FmtPlayer(_order[i]));
                if (_order[i] == currentPlacerNetId) sb.Append(" (CURRENT)");
            }
        }
        Debug.Log(sb.ToString());
    }

    void LogPlacerChanged(uint newId, string via)
    {
        if (!debugLogs) return;
        Debug.Log($"[Turn] Current placer set {via}: {FmtPlayer(newId)}");
    }

}
