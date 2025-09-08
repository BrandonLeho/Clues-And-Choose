using Mirror;
using UnityEngine;

public class PlayerColorChooser : NetworkBehaviour
{
    [Header("UI (local-only)")]
    public SelectionController picker;   // assign on the local player (or find at runtime)

    ColorReservationManager _mgr;
    int _lastVersion = -1;

    void Start()
    {
        _mgr = ColorReservationManager.Instance;

        if (isLocalPlayer && picker)
            picker.onColorConfirmed.AddListener(OnLocalConfirm);

        // Initial paint when we join
        RefreshFromReservations(force: true);
    }

    void OnDestroy()
    {
        if (isLocalPlayer && picker)
            picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
    }

    void Update()
    {
        // Passive version check—works across Mirror versions
        if (_mgr == null) _mgr = ColorReservationManager.Instance;
        if (_mgr && _mgr.version != _lastVersion)
        {
            RefreshFromReservations();
            _lastVersion = _mgr.version;
        }
    }

    // -------- Local → Server --------
    void OnLocalConfirm(Color _, int swatchIndex)
    {
        if (!isLocalPlayer) return;
        CmdConfirmColor(swatchIndex);
    }

    [Command]
    void CmdConfirmColor(int swatchIndex)
    {
        var mgr = ColorReservationManager.Instance;
        if (mgr == null) return;

        int previous;
        bool ok = mgr.TryReserve(netIdentity.netId, swatchIndex, out previous);
        if (!ok)
            TargetReservationDenied(connectionToClient, swatchIndex);
        else
            TargetReservationConfirmed(connectionToClient, swatchIndex, previous);
    }

    [TargetRpc]
    void TargetReservationDenied(NetworkConnectionToClient _, int swatchIndex)
    {
        // Optional UX: toast/SFX. UI stays unchanged because server denied.
        Debug.Log($"Color {swatchIndex} already taken.");
    }

    [TargetRpc]
    void TargetReservationConfirmed(NetworkConnectionToClient _, int swatchIndex, int previousIndex)
    {
        // Optional UX: play a confirm SFX. UI will update via version-sync.
    }

    // -------- Replicated state → local UI --------
    void RefreshFromReservations(bool force = false)
    {
        if (!picker) return;
        if (_mgr == null) return;

        for (int i = 0; i < picker.swatches.Count; i++)
        {
            var s = picker.swatches[i];
            if (!s) continue;

            uint owner; // <-- declare first; no 'out var'
            bool isReserved = _mgr.reservations.TryGetValue(i, out owner);

            if (!isReserved)
            {
                if (s.IsLocked) s.Unlock();
                s.SetSelected(false);
                continue;
            }

            // If it's mine, it will be my locked swatch; if not, it must be unselectable
            bool isMine = (owner == netIdentity.netId) && isLocalPlayer;

            if (!s.IsLocked) s.Lock();    // lock for everyone visually
            if (!isMine) s.SetSelected(false);
        }

        // Confirm button interactivity is already handled by your picker logic.
    }

    public override void OnStopServer()
    {
        // Free reservation on disconnect
        var mgr = ColorReservationManager.Instance;
        if (mgr != null)
            mgr.ReleaseByOwner(netIdentity.netId);
    }
}
