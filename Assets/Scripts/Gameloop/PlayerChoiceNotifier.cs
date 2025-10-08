using Mirror;
using UnityEngine;

public sealed class PlayerChoiceNotifier : NetworkBehaviour
{
    [SyncVar] public bool isClueGiver = true;

    public static PlayerChoiceNotifier Local { get; private set; }

    static bool _serverHookedToRoundManager;

    public override void OnStartAuthority()
    {
        Local = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        var rm = RoundManager.Instance;
        if (rm != null)
        {
            uint currentClueGiver = rm.CurrentClueGiverNetId;
            isClueGiver = netId == currentClueGiver;
        }

        if (!_serverHookedToRoundManager && rm != null)
        {
            RoundManager.OnServerClueGiverChanged += Server_OnClueGiverChanged;
            _serverHookedToRoundManager = true;
        }
    }

    public override void OnStopServer()
    {
        if (_serverHookedToRoundManager)
        {
            RoundManager.OnServerClueGiverChanged -= Server_OnClueGiverChanged;
            _serverHookedToRoundManager = false;
        }
        base.OnStopServer();
    }

    [Server]
    static void Server_OnClueGiverChanged(uint newClueGiverNetId)
    {
        foreach (var kv in NetworkServer.spawned)
        {
            var identity = kv.Value;
            if (!identity) continue;

            var notifier = identity.GetComponent<PlayerChoiceNotifier>();
            if (!notifier) continue;

            notifier.isClueGiver = (identity.netId == newClueGiverNetId);
        }
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
