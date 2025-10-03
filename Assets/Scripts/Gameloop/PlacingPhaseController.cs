using Mirror;
using UnityEngine;

public sealed class PlacingPhaseController : NetworkBehaviour
{
    public static PlacingPhaseController Instance { get; private set; }

    void Awake() => Instance = this;

    [Command(requiresAuthority = false)]
    public void CmdStartPlacingPhase()
    {
        if (CoinPlacementTurnManager.Instance)
        {
            CoinPlacementTurnManager.Instance.ServerBeginCycleAtFirst();
        }

        RpcEnablePerTurnLocks();
    }

    [ClientRpc]
    void RpcEnablePerTurnLocks()
    {
        var binder = FindFirstObjectByType<CoinTurnLockBinder>();
        if (binder) binder.SetModeActive(true);
    }

    [ClientRpc]
    public void RpcDisablePerTurnLocks()
    {
        var binder = FindFirstObjectByType<CoinTurnLockBinder>();
        if (binder) binder.SetModeActive(false);

        var mgr = CoinRoundLockManager.Instance;
        if (mgr) mgr.UnlockAllCoins();
    }
}
