using Mirror;
using UnityEngine;
using System.Collections;

public sealed class CoinTurnLockBinder : MonoBehaviour
{
    [SerializeField] bool debugLogs = true;
    bool _modeActive;
    bool _hasTargetOverride;

    Coroutine _delayedRefreshCo;

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        PhaseController.OnClientTargetChosen += HandleTargetChosen;

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.AddListener(HandleRoundChanged);
            RoundManager.Instance.onClueGiverChangedClient.AddListener(HandleClueGiverChanged);
        }

        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientTargetChosen -= HandleTargetChosen;

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.RemoveListener(HandleRoundChanged);
            RoundManager.Instance.onClueGiverChangedClient.RemoveListener(HandleClueGiverChanged);
        }

        _hasTargetOverride = false;

        if (_delayedRefreshCo != null)
        {
            StopCoroutine(_delayedRefreshCo);
            _delayedRefreshCo = null;
        }
    }

    public void SetModeActive(bool active)
    {
        _modeActive = active;
        if (debugLogs) Debug.Log($"[TurnLock] ModeActive={_modeActive}");

        if (_modeActive) _hasTargetOverride = false;

        if (_delayedRefreshCo != null)
        {
            StopCoroutine(_delayedRefreshCo);
            _delayedRefreshCo = null;
        }

        if (!_modeActive)
        {
            UnlockAllLocal();
            return;
        }

        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);

        _delayedRefreshCo = StartCoroutine(CoDelayedRefreshPlacer());
    }

    void HandleTargetChosen(int col, int row, Color color)
    {
        if (!_modeActive) return;
        _hasTargetOverride = true;
        if (CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void HandleRoundChanged(int _, uint __)
    {
        if (debugLogs) Debug.Log("[TurnLock] Round changed → clear target gate and LOCK.");
        _hasTargetOverride = false;
        LockAllLocal();
        if (_modeActive && CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }
    void HandleClueGiverChanged(uint ___)
    {
        if (debugLogs) Debug.Log("[TurnLock] Clue giver changed → clear target gate and LOCK.");
        _hasTargetOverride = false;
        LockAllLocal();
        if (_modeActive && CoinPlacementTurnManager.Instance)
            HandlePlacerChanged(CoinPlacementTurnManager.Instance.currentPlacerNetId);
    }

    void HandlePlacerChanged(uint currentPlacerNetId)
    {
        if (!_modeActive) return;

        bool hasTarget = _hasTargetOverride || (PhaseController.Instance && PhaseController.Instance.ClientHasTarget);
        if (!hasTarget)
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

    IEnumerator CoDelayedRefreshPlacer()
    {
        yield return null;

        if (!_modeActive)
        {
            _delayedRefreshCo = null;
            yield break;
        }

        var mgr = CoinPlacementTurnManager.Instance;
        if (mgr != null)
        {
            if (debugLogs)
                Debug.Log($"[TurnLock] Delayed refresh → currentPlacerNetId={mgr.currentPlacerNetId}");

            HandlePlacerChanged(mgr.currentPlacerNetId);
        }

        _delayedRefreshCo = null;
    }

    static void LockAllLocal() { var mgr = CoinRoundLockManager.Instance; if (mgr) mgr.LockAllCoins(); }
    static void UnlockAllLocal() { var mgr = CoinRoundLockManager.Instance; if (mgr) mgr.UnlockAllCoins(); }
}
