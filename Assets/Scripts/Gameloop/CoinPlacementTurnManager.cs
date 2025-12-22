using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class CoinPlacementTurnManager : NetworkBehaviour
{
    public static CoinPlacementTurnManager Instance;

    public static System.Action<uint> OnPlacerChangedClient;

    public static event System.Action OnServerFirstCycleCompleted;

    [Header("Debug")]
    [SerializeField] bool debugLogs = true;

    [SyncVar(hook = nameof(OnPlacerChanged_Hook))]
    public uint currentPlacerNetId;

    List<uint> _order = new();
    HashSet<uint> _placedThisCycle = new();
    List<uint> _currentCyclePlacedOrder = new();
    List<uint> _lastCompletedOrder = null;

    int _idx = -1;
    bool _firstCycleComplete = false;

    void Awake() => Instance = this;

    [Server]
    public void ServerBeginCycleAtFirst(bool useReverseOfLast = false)
    {
        _firstCycleComplete = false;
        _placedThisCycle.Clear();
        _currentCyclePlacedOrder.Clear();

        if (useReverseOfLast && _lastCompletedOrder != null && _lastCompletedOrder.Count > 0)
        {
            _order = new List<uint>(_lastCompletedOrder);
            _order.Reverse();
            if (debugLogs) Log("[Turn] Using REVERSED last cycle order: " + string.Join(", ", _order));
        }
        else
        {
            BuildOrder();
        }

        if (_order.Count == 0)
        {
            SetPlacer(0);
            Log("[Turn] Begin: no eligible placers (empty order).");
            _firstCycleComplete = true;
            OnServerFirstCycleCompleted?.Invoke();
            return;
        }

        if (!GameRuleSettings.IsLockAllEnabled)
        {
            _idx = -1;
            SetPlacer(0);
            LogOrder("begin (simultaneous)");
            return;
        }

        _idx = 0;
        SetPlacer(_order[_idx]);
        LogOrder("begin");
    }

    [Server]
    public void ServerNoteSuccessfulPlacement(uint playerNetId)
    {
        if (!GameRuleSettings.IsLockAllEnabled)
        {
            if (_firstCycleComplete) return;

            if (_order.Count == 0)
            {
                Log("[Turn] NotePlacement (simultaneous): empty order.");
                return;
            }

            if (_order.Contains(playerNetId))
            {
                _placedThisCycle.Add(playerNetId);
                if (!_currentCyclePlacedOrder.Contains(playerNetId))
                    _currentCyclePlacedOrder.Add(playerNetId);

                if (NetworkServer.spawned.TryGetValue(playerNetId, out var identity) && identity != null)
                {
                    var conn = identity.connectionToClient;
                    if (conn != null)
                        TargetLockAllCoinsForLocalPlayer(conn);
                }
            }

            if (_placedThisCycle.Count >= _order.Count)
            {
                _firstCycleComplete = true;
                _lastCompletedOrder = new List<uint>(_currentCyclePlacedOrder);
                Log("[Turn] First cycle COMPLETE (simultaneous).");
                SetPlacer(0);
                OnServerFirstCycleCompleted?.Invoke();
            }

            return;
        }

        if (_firstCycleComplete) return;

        if (_order.Count == 0)
        {
            Log("[Turn] NotePlacement: empty order.");
            return;
        }

        if (_order.Contains(playerNetId))
        {
            _placedThisCycle.Add(playerNetId);
            if (!_currentCyclePlacedOrder.Contains(playerNetId))
                _currentCyclePlacedOrder.Add(playerNetId);
        }

        if (_placedThisCycle.Count >= _order.Count)
        {
            _firstCycleComplete = true;
            _lastCompletedOrder = new List<uint>(_currentCyclePlacedOrder);
            Log("[Turn] First cycle COMPLETE. Stopping turn (no active placer).");
            SetPlacer(0);
            OnServerFirstCycleCompleted?.Invoke();
            return;
        }

        int start = Mathf.Clamp(_order.IndexOf(currentPlacerNetId) + 1, 0, _order.Count - 1);
        for (int step = 0; step < _order.Count; step++)
        {
            int nxt = (start + step) % _order.Count;
            uint cand = _order[nxt];
            if (!_placedThisCycle.Contains(cand))
            {
                _idx = nxt;
                SetPlacer(cand);
                LogOrder("advance");
                return;
            }
        }

        _firstCycleComplete = true;
        Log("[Turn] First cycle COMPLETE (fallback).");
        SetPlacer(0);
        OnServerFirstCycleCompleted?.Invoke();
    }

    [Server]
    public bool ServerCanPlayerPlace(uint playerNetId)
    {
        if (!GameRuleSettings.IsLockAllEnabled)
        {
            if (_firstCycleComplete) return false;
            if (_order.Count == 0) return false;
            if (!_order.Contains(playerNetId)) return false;
            if (_placedThisCycle.Contains(playerNetId)) return false;

            return true;
        }

        return !_firstCycleComplete && _order.Count > 0 && playerNetId == currentPlacerNetId;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RoundManager.OnServerRosterChanged += RebuildOrderAndResetIfNeeded;
        RoundManager.OnServerClueGiverChanged += _ => RebuildOrderAndResetIfNeeded();
        BuildOrder();
    }

    public override void OnStopServer()
    {
        RoundManager.OnServerRosterChanged -= RebuildOrderAndResetIfNeeded;
        RoundManager.OnServerClueGiverChanged -= _ => RebuildOrderAndResetIfNeeded();
        base.OnStopServer();
    }

    [Server]
    void RebuildOrderAndResetIfNeeded()
    {
        _firstCycleComplete = false;
        _placedThisCycle.Clear();
        _currentCyclePlacedOrder.Clear();
        _lastCompletedOrder = null;

        BuildOrder();
        if (_order.Count == 0)
        {
            SetPlacer(0);
        }
        else
        {
            if (GameRuleSettings.IsLockAllEnabled)
            {
                _idx = 0;
                SetPlacer(_order[_idx]);
            }
            else
            {
                _idx = -1;
                SetPlacer(0);
            }
        }
        LogOrder("rebuild/reset");
    }

    [Server]
    void BuildOrder()
    {
        _order.Clear();
        var roster = RoundManager.Instance ? RoundManager.Instance.ServerGetRosterSnapshot() : new List<uint>();
        uint clue = RoundManager.Instance ? RoundManager.Instance.ServerGetClueGiverNetIdUnsafe() : 0;
        foreach (var id in roster)
            if (id != clue) _order.Add(id);
        if (debugLogs) Log($"[Turn] Built order (non–clue givers): {string.Join(", ", _order)}");
    }

    void SetPlacer(uint id) => currentPlacerNetId = id;

    void OnPlacerChanged_Hook(uint _, uint newId)
    {
        if (debugLogs) Log($"[Turn] Current placer: {Fmt(newId)}");
        OnPlacerChangedClient?.Invoke(newId);
    }

    void Log(string msg) { if (debugLogs) Debug.Log(msg); }

    void LogOrder(string reason)
    {
        if (!debugLogs) return;
        string Seq() => _order.Count == 0 ? "(empty)" :
            string.Join(" → ", _order.Select(x => x == currentPlacerNetId ? $"{Fmt(x)} (CURRENT)" : Fmt(x)));
        Debug.Log($"[Turn] Order after {reason}: {Seq()}  | placed:{_placedThisCycle.Count}/{_order.Count}");
    }

    static string Fmt(uint id)
    {
        if (id == 0) return "(none)";
        if (NetworkServer.active && NetworkServer.spawned.TryGetValue(id, out var srv)) return $"{srv.gameObject.name}[{id}]";
        if (NetworkClient.active && NetworkClient.spawned.TryGetValue(id, out var cli)) return $"{cli.gameObject.name}[{id}]";
        return $"[{id}]";
    }

    public static bool IsLocalPlayersTurn()
    {
        var me = NetworkClient.connection?.identity;
        return me && Instance && Instance.currentPlacerNetId != 0 && me.netId == Instance.currentPlacerNetId;
    }

    [Server]
    public void ServerForceDropOnCurrentPlacer()
    {
        if (currentPlacerNetId == 0) return;

        if (!NetworkServer.spawned.TryGetValue(currentPlacerNetId, out var identity) || !identity)
            return;

        var conn = identity.connectionToClient;
        if (conn == null) return;

        if (debugLogs)
            Log($"[Turn] ServerForceDropOnCurrentPlacer → sending drop request to {Fmt(currentPlacerNetId)}");

        TargetForceDropIfDragging(conn);
    }

    [Server]
    public void ServerForceCompleteSimultaneousCycleFromTimer()
    {
        if (GameRuleSettings.IsLockAllEnabled)
            return;

        if (_firstCycleComplete)
        {
            if (debugLogs)
                Log("[Turn] ForceComplete(simultaneous): already complete, ignoring.");
            return;
        }

        if (debugLogs)
            Log("[Turn] ForceComplete(simultaneous): timer expired, completing cycle.");

        var finalOrder = new List<uint>();

        foreach (var id in _currentCyclePlacedOrder)
        {
            if (!finalOrder.Contains(id))
                finalOrder.Add(id);
        }

        foreach (var id in _order)
        {
            if (!finalOrder.Contains(id))
                finalOrder.Add(id);
        }

        _lastCompletedOrder = finalOrder;
        _firstCycleComplete = true;

        SetPlacer(0);

        OnServerFirstCycleCompleted?.Invoke();
    }

    [TargetRpc]
    void TargetForceDropIfDragging(NetworkConnection target)
    {
        if (debugLogs)
            Log("[Turn] TargetForceDropIfDragging → CoinDragHandler.ForceDropIfDragging() on client");

        CoinDragHandler.ForceDropIfDragging();
    }

    [TargetRpc]
    void TargetLockAllCoinsForLocalPlayer(NetworkConnection target)
    {
        if (debugLogs)
            Log("[Turn] TargetLockAllCoinsForLocalPlayer → CoinRoundLockManager.LockAllCoins() on client");

        var mgr = CoinRoundLockManager.Instance;
        if (mgr) mgr.LockAllCoins();
    }
}
