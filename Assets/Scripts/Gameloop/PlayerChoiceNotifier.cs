using Mirror;
using UnityEngine;

public sealed class PlayerChoiceNotifier : NetworkBehaviour
{
    [SyncVar] public bool isClueGiver = true;

    public static PlayerChoiceNotifier Local { get; private set; }

    public override void OnStartAuthority()
    {
        Local = this;
    }

    public void NotifyChoiceSelected()
    {
        if (!isLocalPlayer) return;
        CmdChoiceSelected();
    }

    [Command]
    void CmdChoiceSelected()
    {
        if (!isClueGiver) return;
        RpcUnlockAllCoins();
    }

    [ClientRpc]
    void RpcUnlockAllCoins()
    {
        if (ClueGiverState.IsLocalPlayerClueGiver())
        {
            return;
        }

        var mgr = FindFirstObjectByType<CoinRoundLockManager>();
        if (mgr) mgr.UnlockAllCoins();
    }

    void RpcOnChoiceSelected_UnlockPhase()
    {
        var lockMgr = FindFirstObjectByType<CoinRoundLockManager>();
        if (!lockMgr) return;

        if (GameRuleSettings.IsLockAllEnabled)
        {
            if (!ClueGiverState.IsLocalPlayerClueGiver())
                lockMgr.UnlockAllCoins();
        }
        else
        {
            lockMgr.UnlockAllCoins();
        }
    }
}
