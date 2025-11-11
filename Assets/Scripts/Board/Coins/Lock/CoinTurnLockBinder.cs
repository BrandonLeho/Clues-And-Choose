// CoinTurnLockBinder.cs
using Mirror;
using UnityEngine;

public sealed class CoinTurnLockBinder : MonoBehaviour
{
    [SerializeField] bool debugLogs = true;
    bool _modeActive;

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        PhaseController.OnClientTargetChosen += HandleTargetChosen;

        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientTargetChosen -= HandleTargetChosen;
    }

    public void SetModeActive(bool active)
    {
        _modeActive = active;
        if (debugLogs) Debug.Log($"[TurnLock] ModeActive={_modeActive}");
        if (!_modeActive) { UnlockAllLocal(); return; }
        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void HandleTargetChosen(int col, int row, Color color)
    {
        if (!_modeActive) return;
        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void HandlePlacerChanged(uint currentPlacerNetId)
    {
        if (!_modeActive) return;

        if (!(PhaseController.Instance && PhaseController.Instance.ClientHasTarget))
        {
            if (debugLogs) Debug.Log("[TurnLock] No target yet → keep ALL coins LOCKED.");
            LockAllLocal();
            return;
        }

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
