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
            RpcShowEndRoundPrompt();
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

        if (endNow)
        {
            RpcHideEndRoundPrompt();
            ServerBeginScoring();
        }
        else
        {
            RpcHideEndRoundPrompt();
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
        Debug.Log("[Phase] First placement cycle finished → All players locked, awaiting clue giver choice.");
    }

    [ClientRpc]
    void RpcShowEndRoundPrompt()
    {
        EndRoundPromptUI.Instance?.Show();
    }

    [ClientRpc]
    void RpcHideEndRoundPrompt()
    {
        EndRoundPromptUI.Instance?.Hide();
    }

    [Server]
    void ServerBeginScoring()
    {
        Debug.Log("[Phase] Begin SCORING (stub) — coins remain locked.");
        // TODO: plug in scoring flow here
        // RpcHideEndRoundPrompt();
    }
}
