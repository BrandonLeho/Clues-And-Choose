using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class GameLoopManager : NetworkBehaviour
{
    public static GameLoopManager Instance { get; private set; }
    public static bool Exists => Instance != null;

    [Header("Config")]
    public GameSettings settings;

    [Header("Events (UI hooks)")]
    public UnityEvent<string> OnClueGiverChanged;
    public UnityEvent<string> OnPhaseChanged;
    public UnityEvent<string, int> OnScoreChanged;
    public UnityEvent<string> OnGameWinner;
    public UnityEvent<List<string>> OnRosterRefreshed;

    public enum Phase { Lobby, RoundSetup, Clue, Guess, Scoring, RoundEnd, GameEnd }

    [SyncVar] private Phase _phase = Phase.Lobby;
    [SyncVar] private int _roundNumber = 0;

    [Serializable]
    public struct PlayerEntry
    {
        public uint netId;
        public string name;
        public int score;
    }

    public class SyncListPlayerEntry : SyncList<PlayerEntry> { }
    public SyncListPlayerEntry players = new SyncListPlayerEntry();

    [SyncVar] private int _clueGiverIndex = -1;

    private double _phaseEndTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        players.Callback += OnPlayersSync;
        Server_RebuildInitialRosterIfAvailable();
        Server_SetPhase(Phase.Lobby, 0);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        players.Callback -= OnPlayersSync;
    }

    [Command(requiresAuthority = false)]
    public void CmdSetFirstClueGiverByName(string winnerName)
    {
        if (!isServer) return;
        int idx = players.FindIndex(p => string.Equals(p.name, winnerName, StringComparison.Ordinal));
        if (idx < 0 && players.Count > 0) idx = 0;
        _clueGiverIndex = idx;
        Server_StartNewRound(resetRoundNumber: true);
    }

    [Command(requiresAuthority = false)]
    public void CmdAdvancePhase()
    {
        if (!isServer) return;
        Server_AdvancePhase();
    }

    [Server]
    public void Server_AwardPoints(uint targetNetId, int delta)
    {
        int i = players.FindIndex(p => p.netId == targetNetId);
        if (i < 0) return;
        var pe = players[i];
        pe.score = Mathf.Max(0, pe.score + delta);
        players[i] = pe;
        RpcNotifyScoreChanged(pe.name, pe.score);

        if (pe.score >= settings.targetScore)
        {
            Server_SetPhase(Phase.GameEnd, 0);
            RpcNotifyWinner(pe.name);
        }
    }

    [Server]
    public void Server_OnPlayerLeft(NetPlayerInfo info)
    {
        int i = players.FindIndex(p => p.netId == info.netId);
        if (i < 0) return;

        bool wasClueGiver = (i == _clueGiverIndex);
        players.RemoveAt(i);

        if (players.Count == 0)
        {
            _clueGiverIndex = -1;
            Server_SetPhase(Phase.Lobby, 0);
            return;
        }

        if (_clueGiverIndex >= players.Count) _clueGiverIndex = players.Count - 1;
        if (wasClueGiver)
        {
            _clueGiverIndex = _clueGiverIndex % players.Count;
            RpcNotifyClueGiver(players[_clueGiverIndex].name);
        }

        Server_BroadcastRoster();
    }

    [Server]
    public void Server_AddLateJoiner(NetPlayerInfo info, int initialScore = 0)
    {
        int existing = players.FindIndex(p => p.netId == info.netId);
        if (existing >= 0) return;

        players.Add(new PlayerEntry { netId = info.netId, name = info.playerName, score = initialScore });

        if (_clueGiverIndex < 0 && players.Count > 0)
            _clueGiverIndex = 0;

        Server_BroadcastRoster();
    }

    [Server]
    private void Server_RebuildInitialRosterIfAvailable()
    {
        foreach (var kv in NetworkServer.spawned)
        {
            if (kv.Value != null && kv.Value.TryGetComponent(out NetPlayerInfo info))
            {
                players.Add(new PlayerEntry { netId = info.netId, name = info.playerName, score = 0 });
            }
        }

        if (players.Count > 0 && _clueGiverIndex < 0)
            _clueGiverIndex = 0;

        Server_BroadcastRoster();
    }

    private void OnPlayersSync(SyncList<PlayerEntry>.Operation op, int index, PlayerEntry oldItem, PlayerEntry newItem)
    {
        if (!isClient) return;
        var names = new List<string>(players.Count);
        for (int i = 0; i < players.Count; i++) names.Add(players[i].name);
        OnRosterRefreshed?.Invoke(names);
    }

    [Server]
    private void Server_BroadcastRoster()
    {
        var names = new List<string>(players.Count);
        for (int i = 0; i < players.Count; i++) names.Add(players[i].name);
        RpcRoster(names);
    }

    [ClientRpc]
    private void RpcRoster(List<string> names)
    {
        OnRosterRefreshed?.Invoke(names);
    }

    private void Update()
    {
        if (!isServer) return;
        if (_phase == Phase.GameEnd || _phase == Phase.Lobby) return;

        if (NetworkTime.time >= _phaseEndTime)
        {
            Server_AdvancePhase();
        }
    }

    [Server]
    private void Server_AdvancePhase()
    {
        switch (_phase)
        {
            case Phase.RoundSetup:
                Server_SetPhase(Phase.Clue, settings.phaseDuration_Clue);
                break;
            case Phase.Clue:
                Server_SetPhase(Phase.Guess, settings.phaseDuration_Guess);
                break;
            case Phase.Guess:
                Server_SetPhase(Phase.Scoring, settings.phaseDuration_Scoring);
                break;
            case Phase.Scoring:
                Server_SetPhase(Phase.RoundEnd, 2f);
                break;
            case Phase.RoundEnd:
                Server_StartNextRoundOrEnd();
                break;
        }
    }

    [Server]
    private void Server_SetPhase(Phase p, float durationSeconds)
    {
        _phase = p;
        _phaseEndTime = (durationSeconds > 0) ? NetworkTime.time + durationSeconds : double.MaxValue;
        RpcNotifyPhase(p.ToString());
    }

    [Server]
    private void Server_StartNewRound(bool resetRoundNumber)
    {
        if (resetRoundNumber) _roundNumber = 0;
        Server_StartNextRoundOrEnd();
    }

    [Server]
    private void Server_StartNextRoundOrEnd()
    {
        _roundNumber++;
        if (_roundNumber > settings.maxRounds)
        {
            string winner = players.Count > 0 ? players[0].name : "";
            int best = int.MinValue;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].score > best)
                {
                    best = players[i].score;
                    winner = players[i].name;
                }
            }
            Server_SetPhase(Phase.GameEnd, 0);
            RpcNotifyWinner(winner);
            return;
        }

        if (settings.rotateClueGiverEveryRound && players.Count > 0)
            _clueGiverIndex = (_clueGiverIndex + 1) % players.Count;

        if (players.Count == 0)
        {
            Server_SetPhase(Phase.Lobby, 0);
            return;
        }

        RpcNotifyClueGiver(players[_clueGiverIndex].name);
        Server_SetPhase(Phase.RoundSetup, 2f);
    }

    [ClientRpc] private void RpcNotifyPhase(string phaseName) => OnPhaseChanged?.Invoke(phaseName);
    [ClientRpc] private void RpcNotifyClueGiver(string name) => OnClueGiverChanged?.Invoke(name);
    [ClientRpc] private void RpcNotifyScoreChanged(string name, int score) => OnScoreChanged?.Invoke(name, score);
    [ClientRpc] private void RpcNotifyWinner(string name) => OnGameWinner?.Invoke(name);

    public bool Client_IsLocalClueGiver(uint localNetId)
    {
        if (players == null || players.Count == 0) return false;
        int idx = _clueGiverIndex;
        if (idx < 0 || idx >= players.Count) return false;
        return players[idx].netId == localNetId;
    }
}
