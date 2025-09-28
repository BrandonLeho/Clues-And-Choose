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

    [Header("Events (Client-side)")]
    public UnityEvent<int, uint> onRoundChangedClient;
    public UnityEvent<uint> onClueGiverChangedClient;

    [Serializable]
    public class CardChoiceEvent : UnityEvent<int, int, Color> { }
    public CardChoiceEvent onCardChoiceSetClient;

    [Serializable]
    public struct PlacementComparedPayload
    {
        public uint coinNetId;
        public int spotCol, spotRow;
        public int cardCol, cardRow;
        public int manhattan;
        public float euclidean;
    }

    [Serializable]
    public class PlacementComparedEvent : UnityEvent<PlacementComparedPayload> { }
    public PlacementComparedEvent onPlacementComparedClient;

    readonly SyncList<uint> _roster = new SyncList<uint>();

    [SyncVar(hook = nameof(OnRoundIndexChanged))] int _roundIndex = -1;
    [SyncVar(hook = nameof(OnClueGiverNetIdChanged))] uint _clueGiverNetId;
    [SyncVar] int _clueGiverRosterIndex = -1;

    public int CurrentRoundIndex => _roundIndex;
    public uint CurrentClueGiverNetId => _clueGiverNetId;

    [SyncVar(hook = nameof(OnCardColChanged))] int _cardCol = -1;
    [SyncVar(hook = nameof(OnCardRowChanged))] int _cardRow = -1;
    [SyncVar(hook = nameof(OnCardColorChanged))] Color _cardColor = Color.clear;

    HashSet<uint> _placedThisRound = new HashSet<uint>();

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
        }
        else if (_roster.Count == 0)
        {
            _clueGiverRosterIndex = -1;
            SetClueGiverNetId(0);
        }
    }

    void OnRosterChanged(SyncList<uint>.Operation op, int index, uint oldItem, uint newItem)
    {
        // TODO: UI update on roster change
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
            RpcNotifyRoundStarted(_roundIndex, _clueGiverNetId);
        }
    }

    [Server]
    public void ServerAdvanceRound()
    {
        if (_roster.Count == 0) return;

        _roundIndex = Mathf.Max(0, _roundIndex) + 1;

        _clueGiverRosterIndex = (_clueGiverRosterIndex + 1 + _roster.Count) % _roster.Count;
        SetClueGiverByRosterIndex(_clueGiverRosterIndex);

        _cardCol = -1;
        _cardRow = -1;
        _cardColor = Color.clear;
        ServerResetPlacementsForRound();

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


    void OnCardColChanged(int _, int newVal) => RaiseCardChoiceSet();
    void OnCardRowChanged(int _, int newVal) => RaiseCardChoiceSet();
    void OnCardColorChanged(Color _, Color __) => RaiseCardChoiceSet();

    void RaiseCardChoiceSet()
    {
        if (_cardCol >= 0 && _cardRow >= 0)
            onCardChoiceSetClient?.Invoke(_cardCol, _cardRow, _cardColor);
    }

    public void ClientReportCardChoice(int col, int row, Color color)
    {
        if (!NetworkClient.active) return;
        CmdSetCardChoice(col, row, color);
    }

    public void ClientReportCoinPlaced(uint coinNetId, int spotIndex)
    {
        if (!NetworkClient.active) return;
        CmdReportCoinPlaced(coinNetId, spotIndex);
    }

    [Command(requiresAuthority = false)]
    void CmdSetCardChoice(int col, int row, Color color, NetworkConnectionToClient sender = null)
    {
        _cardCol = Mathf.Max(0, col);
        _cardRow = Mathf.Max(0, row);
        _cardColor = color;

        uint setter = sender?.identity ? sender.identity.netId : 0u;
        Debug.Log($"[CardChoice] ClueGiver={_clueGiverNetId} SetBy={setter} -> cell=({_cardCol},{_cardRow}) color={_cardColor}");

        ServerResetPlacementsForRound();

        RpcCardChoiceSet(_cardCol, _cardRow, _cardColor);
    }


    [ClientRpc]
    void RpcCardChoiceSet(int col, int row, Color color)
    {
        onCardChoiceSetClient?.Invoke(col, row, color);
    }

    [Command(requiresAuthority = false)]
    void CmdReportCoinPlaced(uint coinNetId, int spotIndex, NetworkConnectionToClient sender = null)
    {
        if (_cardCol < 0 || _cardRow < 0) return;

        uint placerPlayerNetId = sender?.identity ? sender.identity.netId : 0u;

        int spotCol, spotRow;
        if (BoardSpotsNet.Instance == null || !BoardSpotsNet.Instance.TryGetSpotCoord(spotIndex, out spotCol, out spotRow))
            return;

        int dx = Mathf.Abs(spotCol - _cardCol);
        int dy = Mathf.Abs(spotRow - _cardRow);
        int manhattan = dx + dy;
        float euclid = Mathf.Sqrt(dx * dx + dy * dy);

        Debug.Log($"[Placement] Player={placerPlayerNetId} Coin={coinNetId} Spot=({spotCol},{spotRow}) vs Card=({_cardCol},{_cardRow}) -> Manhattan={manhattan} Euclid={euclid:0.###}");

        if (placerPlayerNetId != 0 && placerPlayerNetId != _clueGiverNetId)
        {
            _placedThisRound.Add(placerPlayerNetId);

            int nonClueCount = Mathf.Max(0, _roster.Count - (_clueGiverNetId != 0 ? 1 : 0));
            int remaining = Mathf.Max(0, nonClueCount - _placedThisRound.Count);

            Debug.Log($"[PlacementProgress] {_placedThisRound.Count} / {nonClueCount} non-clue-givers placed. Remaining={remaining}");

            if (_placedThisRound.Count == nonClueCount && nonClueCount > 0)
            {
                Debug.Log("[PlacementComplete] All non clue-giver players have placed their coins.");
            }
        }

        RpcPlacementCompared(coinNetId, spotCol, spotRow, _cardCol, _cardRow, manhattan, euclid);
    }


    [ClientRpc]
    void RpcPlacementCompared(uint coinNetId, int spotCol, int spotRow, int cardCol, int cardRow, int manhattan, float euclidean)
    {
        var payload = new PlacementComparedPayload
        {
            coinNetId = coinNetId,
            spotCol = spotCol,
            spotRow = spotRow,
            cardCol = cardCol,
            cardRow = cardRow,
            manhattan = manhattan,
            euclidean = euclidean
        };
        onPlacementComparedClient?.Invoke(payload);
    }

    [Server]
    void ServerResetPlacementsForRound()
    {
        _placedThisRound.Clear();
    }
}
