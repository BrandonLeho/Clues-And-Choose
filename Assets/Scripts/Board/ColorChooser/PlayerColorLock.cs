using Mirror;
using UnityEngine;

public class PlayerColorLock : NetworkBehaviour
{
    [Header("UI (optional; auto-find if empty)")]
    public SelectionController selectionController;

    ColorReservationManager _mgr;

    public override void OnStartLocalPlayer()
    {
        // find UI if not assigned
        if (!selectionController) selectionController = FindObjectOfType<SelectionController>(true);
        Debug.Log(selectionController);
        _mgr = ColorReservationManager.Instance;

        if (!_mgr || !selectionController)
            return;

        // Replace the default local confirm behavior with a networked one
        if (selectionController.confirmButton)
        {
            selectionController.confirmButton.onClick.RemoveAllListeners();
            selectionController.confirmButton.onClick.AddListener(OnConfirmClicked_Local);
        }

        // React to reservation map changes
        _mgr.OnStateChanged += RefreshUIFromNetwork;
        RefreshUIFromNetwork();
    }

    void OnDestroy()
    {
        if (isLocalPlayer && _mgr != null)
            _mgr.OnStateChanged -= RefreshUIFromNetwork;
    }

    // ---------- Local confirm → server request ----------
    void OnConfirmClicked_Local()
    {
        if (!isLocalPlayer || selectionController == null) return;
        if (!selectionController.TryGetCurrentSwatch(out var swatch)) return;

        int idx = selectionController.swatches.IndexOf(swatch);
        if (idx < 0) return;

        CmdRequestLock(idx);
    }

    [Command]
    void CmdRequestLock(int swatchIndex)
    {
        var ok = ColorReservationManager.Instance.TryLock(swatchIndex, netIdentity);
        TargetConfirmResult(connectionToClient, ok, swatchIndex);
    }

    [TargetRpc]
    void TargetConfirmResult(NetworkConnection target, bool ok, int swatchIndex)
    {
        // If it succeeded, the SyncDictionary will change and RefreshUIFromNetwork will update visuals.
        // If it failed, you could shake the swatch, beep, etc. (optional)
        if (!ok)
        {
            // Simple optional nudge:
            // Debug.Log("That color is already taken.");
        }
    }

    // ---------- UI refresh for EVERY change in reservations ----------
    void RefreshUIFromNetwork()
    {
        if (!selectionController || _mgr == null) return;

        // 1) Apply lock/unlock visuals to every swatch
        for (int i = 0; i < selectionController.swatches.Count; i++)
        {
            bool isTaken = _mgr.IsLocked(i);
            var sw = selectionController.swatches[i];
            if (!sw) continue;

            if (isTaken) sw.Lock(); else sw.Unlock();
        }

        // 2) If *this* player owns a lock, mark it as our locked choice in the SelectionController
        int myIdx = _mgr.GetIndexForPlayer(netId);
        if (myIdx >= 0)
        {
            selectionController.ApplyLockFromNetwork(myIdx, asLocalPlayer: true);
        }
        else
        {
            // No lock owned → clear local lock state (keeps selection visuals if any)
            selectionController.ClearLock();
        }
    }
}
