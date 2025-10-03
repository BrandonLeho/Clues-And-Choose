using Mirror;
using UnityEngine;

public sealed class PlacingPhaseController : NetworkBehaviour
{
    public static PlacingPhaseController Instance { get; private set; }
    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        base.OnStartServer();
        CoinPlacementTurnManager.OnServerFirstCycleCompleted += HandleFirstCycleCompleted_Server;
    }

    public override void OnStopServer()
    {
        CoinPlacementTurnManager.OnServerFirstCycleCompleted -= HandleFirstCycleCompleted_Server;
        base.OnStopServer();
    }

    [Command(requiresAuthority = false)]
    public void CmdStartPlacingPhase()
    {
        if (CoinPlacementTurnManager.Instance)
            CoinPlacementTurnManager.Instance.ServerBeginCycleAtFirst();

        RpcEnablePerTurnLocks();
    }

    [Server]
    void HandleFirstCycleCompleted_Server()
    {
        RpcEndFirstCycle_LockAll();
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

    // (Later) When the clue giver decides:
    // [Command] public void CmdClueGiverNextRound()
    // [Command] public void CmdClueGiverEndRound()
}
