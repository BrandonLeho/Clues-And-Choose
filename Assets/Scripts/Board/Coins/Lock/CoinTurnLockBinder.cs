using Mirror;
using UnityEngine;

public sealed class CoinTurnLockBinder : MonoBehaviour
{
    [SerializeField] bool debugLogs = true;
    bool _modeActive;

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }
    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
    }

    public void SetModeActive(bool active)
    {
        _modeActive = active;
        if (debugLogs) Debug.Log($"[TurnLock] ModeActive={_modeActive}");
        if (!_modeActive) { UnlockAllLocal(); return; }
        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void HandlePlacerChanged(uint currentPlacerNetId)
    {
        if (!_modeActive) return;
        var me = NetworkClient.connection?.identity;
        bool myTurn = me && currentPlacerNetId != 0 && me.netId == currentPlacerNetId;
        if (myTurn) UnlockAllLocal();
        else LockAllLocal();
    }

    static void LockAllLocal()
    {
        var mgr = CoinRoundLockManager.Instance;
        if (mgr) mgr.LockAllCoins();
    }
    static void UnlockAllLocal()
    {
        var mgr = CoinRoundLockManager.Instance;
        if (mgr) mgr.UnlockAllCoins();
    }
}
