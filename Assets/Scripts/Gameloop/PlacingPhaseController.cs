using Mirror;
using UnityEngine;

public sealed class PlacingPhaseController : NetworkBehaviour
{
    public static PlacingPhaseController Instance { get; private set; }
    void Awake() => Instance = this;

    [SyncVar] int cyclesCompleted = 0;

    public override void OnStartServer()
    {
        base.OnStartServer();
        CoinPlacementTurnManager.OnServerFirstCycleCompleted += HandleServerCycleCompleted;
    }
    public override void OnStopServer()
    {
        CoinPlacementTurnManager.OnServerFirstCycleCompleted -= HandleServerCycleCompleted;
        base.OnStopServer();
    }

    [Command(requiresAuthority = false)]
    public void CmdStartPlacingPhase()
    {
        cyclesCompleted = 0;
        if (CoinPlacementTurnManager.Instance)
            CoinPlacementTurnManager.Instance.ServerBeginCycleAtFirst();

        RpcEnablePerTurnLocks();
    }

    [Server]
    void HandleServerCycleCompleted()
    {
        cyclesCompleted++;

        if (cyclesCompleted == 1)
        {
            RpcEndFirstCycle_LockAll();

            var cgConn = GetClueGiverConnection();
            if (cgConn != null)
                TargetShowEndRoundPrompt(cgConn);
        }
        else
        {
            ServerBeginScoring();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdClueGiverChoose(bool endNow, NetworkConnectionToClient sender = null)
    {
        if (!IsConnectionClueGiver(sender)) return;

        RpcAnnounceRoundDecision(endNow);

        RpcHideEndRoundPrompt();

        if (endNow)
        {
            ServerBeginScoring();
        }
        else
        {
            if (CoinPlacementTurnManager.Instance)
                CoinPlacementTurnManager.Instance.ServerBeginCycleAtFirst();

            RpcEnablePerTurnLocks();
        }
    }

    [Server]
    bool IsConnectionClueGiver(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.identity == null) return false;
        uint cg = RoundManager.Instance ? RoundManager.Instance.ServerGetClueGiverNetIdUnsafe() : 0u;
        return conn.identity.netId == cg;
    }

    [Server]
    NetworkConnectionToClient GetClueGiverConnection()
    {
        if (!RoundManager.Instance) return null;
        uint cg = RoundManager.Instance.ServerGetClueGiverNetIdUnsafe();
        if (cg == 0 || !NetworkServer.spawned.TryGetValue(cg, out var id)) return null;
        return id.connectionToClient;
    }

    [ClientRpc]
    void RpcEnablePerTurnLocks()
    {
        var binder = FindFirstObjectByType<CoinTurnLockBinder>();
        if (binder) binder.SetModeActive(true);
    }

    [ClientRpc]
    void RpcEndFirstCycle_LockAll()
    {
        var binder = FindFirstObjectByType<CoinTurnLockBinder>();
        if (binder) binder.SetModeActive(false);

        var mgr = CoinRoundLockManager.Instance;
        if (mgr) mgr.LockAllCoins();

        Debug.Log("[Phase] First placement cycle finished → All players locked. Waiting for clue giver decision…");
    }

    [TargetRpc]
    void TargetShowEndRoundPrompt(NetworkConnection target)
    {
        EndRoundPromptUI.Instance?.Show();
        Debug.Log("[Phase] End Round prompt shown (clue giver only).");
    }

    [ClientRpc]
    void RpcHideEndRoundPrompt()
    {
        EndRoundPromptUI.Instance?.Hide();
    }

    [ClientRpc]
    void RpcAnnounceRoundDecision(bool endNow)
    {
        Debug.Log(endNow ? "[Phase] Clue giver chose: END ROUND" : "[Phase] Clue giver chose: ANOTHER CYCLE");
    }

    [Server]
    void ServerBeginScoring()
    {
        Debug.Log("[Phase] Begin SCORING (stub). Coins remain locked.");
    }
}
