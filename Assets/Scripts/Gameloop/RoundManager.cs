using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance;

    [Header("References")]
    public ClueGiverState clueGiverState;

    [Header("Events")]
    public UnityEvent<int, uint> onRoundChangedClient;
    public UnityEvent<uint> onClueGiverChangedClient;

    public static event Action<int, int> OnServerFullCycleCompleted;
    public static event Action OnServerClueGiverCyclesFinished;

    readonly SyncList<uint> _roster = new SyncList<uint>();

    [SyncVar(hook = nameof(OnRoundIndexChanged))] int _roundIndex = -1;
    [SyncVar(hook = nameof(OnClueGiverNetIdChanged))] uint _clueGiverNetId;
    [SyncVar] int _clueGiverRosterIndex = -1;

    [Header("Clue Giver Cycles")]
    [SerializeField, Min(1)] int maxFullCycles = 2;
    [SyncVar] int _fullCyclesCompleted = 0;
    [SyncVar] int _cycleStartIndex = -1;

    public int CurrentRoundIndex => _roundIndex;
    public uint CurrentClueGiverNetId => _clueGiverNetId;
    public int FullCyclesCompleted => _fullCyclesCompleted;
    public int MaxFullCycles => maxFullCycles;

    public static event Action OnServerRosterChanged;
    public static event Action<uint> OnServerClueGiverChanged;

    void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!clueGiverState) clueGiverState = FindFirstObjectByType<ClueGiverState>();
        _roster.Callback += OnRosterChanged;
    }

    public override void OnStopServer()
    {
        _roster.Callback -= OnRosterChanged;
        base.OnStopServer();
    }

    [Server]
    public void ServerRegisterPlayer(uint netId)
    {
        if (!_roster.Contains(netId))
            _roster.Add(netId);
    }

    [Server]
    public void ServerUnregisterPlayer(uint netId)
    {
        int idx = _roster.IndexOf(netId);
        if (idx >= 0) _roster.RemoveAt(idx);

        if (netId == _clueGiverNetId && _roster.Count > 0)
        {
            _clueGiverRosterIndex = Mathf.Clamp(_clueGiverRosterIndex, 0, _roster.Count - 1);
            _clueGiverRosterIndex %= _roster.Count;
            SetClueGiverByRosterIndex(_clueGiverRosterIndex);
            if (_cycleStartIndex < 0 || _cycleStartIndex >= _roster.Count)
                _cycleStartIndex = _clueGiverRosterIndex;
        }
        else if (_roster.Count == 0)
        {
            _clueGiverRosterIndex = -1;
            _cycleStartIndex = -1;
            SetClueGiverNetId(0);
        }
    }

    void OnRosterChanged(SyncList<uint>.Operation op, int index, uint oldItem, uint newItem)
    {
        OnServerRosterChanged?.Invoke();
    }

    [Server]
    public void ServerSetInitialClueGiver(uint winnerNetId)
    {
        if (!_roster.Contains(winnerNetId))
            _roster.Add(winnerNetId);

        _clueGiverRosterIndex = EnsureRosterIndexOf(winnerNetId);
        SetClueGiverByRosterIndex(_clueGiverRosterIndex);

        if (_roundIndex < 0)
        {
            _roundIndex = 0;
            _cycleStartIndex = _clueGiverRosterIndex;
            RpcNotifyRoundStarted(_roundIndex, _clueGiverNetId);
        }
    }

    [Server]
    public void ServerAdvanceRound()
    {
        if (_roster.Count == 0) return;
        if (_fullCyclesCompleted >= maxFullCycles)
        {
            Debug.Log("[Round] Clue-giver cycles finished; not advancing.");
            OnServerClueGiverCyclesFinished?.Invoke();
            return;
        }

        _roundIndex = Mathf.Max(0, _roundIndex) + 1;

        int previousIndex = _clueGiverRosterIndex;
        _clueGiverRosterIndex = (_clueGiverRosterIndex + 1 + _roster.Count) % _roster.Count;

        if (_cycleStartIndex >= 0 && _clueGiverRosterIndex == _cycleStartIndex)
        {
            _fullCyclesCompleted++;
            OnServerFullCycleCompleted?.Invoke(_fullCyclesCompleted, maxFullCycles);
            Debug.Log($"[Round] Full clue-giver cycle completed ({_fullCyclesCompleted}/{maxFullCycles}).");

            if (_fullCyclesCompleted >= maxFullCycles)
            {
                OnServerClueGiverCyclesFinished?.Invoke();
                Debug.Log("[Round] Max clue-giver cycles reached; rounds will no longer advance.");
                return;
            }
            _cycleStartIndex = _clueGiverRosterIndex;
        }

        SetClueGiverByRosterIndex(_clueGiverRosterIndex);
        RpcApplyCardHoverForNewClueGiver(_clueGiverNetId);
        RpcNotifyRoundStarted(_roundIndex, _clueGiverNetId);
    }

    [Server]
    int EnsureRosterIndexOf(uint netId)
    {
        int i = _roster.IndexOf(netId);
        if (i < 0)
        {
            _roster.Add(netId);
            i = _roster.Count - 1;
        }
        return i;
    }

    [Server]
    void SetClueGiverByRosterIndex(int idx)
    {
        if (_roster.Count == 0) return;
        idx = Mathf.Clamp(idx, 0, _roster.Count - 1);
        uint netId = _roster[idx];
        SetClueGiverNetId(netId);
    }

    [Server]
    void SetClueGiverNetId(uint netId)
    {
        _clueGiverNetId = netId;
        if (clueGiverState) clueGiverState.ServerSetClueGiver(netId);
        OnServerClueGiverChanged?.Invoke(netId);
    }

    void OnRoundIndexChanged(int _, int newRound)
    {
        onRoundChangedClient?.Invoke(newRound, _clueGiverNetId);
    }

    void OnClueGiverNetIdChanged(uint _, uint newNetId)
    {
        onClueGiverChangedClient?.Invoke(newNetId);
    }

    [ClientRpc]
    void RpcNotifyRoundStarted(int roundIndex, uint clueGiverNetId)
    {
        onRoundChangedClient?.Invoke(roundIndex, clueGiverNetId);
        onClueGiverChangedClient?.Invoke(clueGiverNetId);
    }

    [Server] public List<uint> ServerGetRosterSnapshot() => new List<uint>(_roster);
    [Server] public uint ServerGetClueGiverNetIdUnsafe() => _clueGiverNetId;

    [ClientRpc]
    void RpcApplyCardHoverForNewClueGiver(uint clueGiverNetId)
    {
        var hover = FindFirstObjectByType<CardHover>();
        if (!hover) return;

        uint localId = 0;
        if (NetworkClient.localPlayer != null)
            localId = NetworkClient.localPlayer.netId;

        if (localId == clueGiverNetId)
        {
            hover.ApplyClueGiverAutoHover();
        }
        else
        {
            hover.RejectHoverForNonClueGiver();
        }
    }
}
