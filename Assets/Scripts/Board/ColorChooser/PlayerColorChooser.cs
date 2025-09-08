using System.Collections;
using Mirror;
using UnityEngine;

public class PlayerColorChooser : NetworkBehaviour
{
    [Header("UI (assigned by binder at runtime)")]
    [SerializeField] SelectionController picker;    // now private-ish; set via SetPicker

    ColorReservationManager _mgr;

    void Start()
    {
        // Subscribe to manager on all clients so we can reflect locks
        TryBindManagerOrWait();
    }

    void OnDestroy()
    {
        if (_mgr != null)
            _mgr.reservations.OnChange -= OnReservationsChanged;

        if (isLocalPlayer && picker != null)
            picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
    }

    // ---------- called by scene-side binder ----------
    [Client]
    public void SetPicker(SelectionController p)
    {
        if (!isLocalPlayer) return;        // only local player drives local UI
        if (picker == p) return;

        // Unbind old
        if (picker) picker.onColorConfirmed.RemoveListener(OnLocalConfirm);

        picker = p;

        if (picker)
        {
            picker.onColorConfirmed.AddListener(OnLocalConfirm);
            // If we already have replicated state, push it into the UI now
            RefreshFromReservations();
        }
    }

    // ---------- Local → Server ----------
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
        if (!ok) { TargetReservationDenied(connectionToClient, swatchIndex); return; }

        TargetReservationConfirmed(connectionToClient, swatchIndex, previous);
    }

    [TargetRpc]
    void TargetReservationDenied(NetworkConnectionToClient _, int idx)
    {
        Debug.Log($"Color {idx} is already taken.");
    }

    [TargetRpc]
    void TargetReservationConfirmed(NetworkConnectionToClient _, int newIdx, int oldIdx)
    {
        // Optional: play SFX/flash
    }

    // ---------- Replication → UI ----------
    void OnReservationsChanged(SyncIDictionary<int, uint>.Operation op, int key, uint item)
    {
        RefreshFromReservations();
    }

    void RefreshFromReservations()
    {
        if (picker == null) return;
        if (_mgr == null) _mgr = ColorReservationManager.Instance;

        for (int i = 0; i < picker.swatches.Count; i++)
        {
            var s = picker.swatches[i];
            if (!s) continue;

            bool reserved = _mgr != null && _mgr.reservations.TryGetValue(i, out uint _);

            if (reserved)
            {
                if (!s.IsLocked) s.Lock();
                s.SetSelected(false);  // ensure not “pending” locally
            }
            else
            {
                if (s.IsLocked) s.Unlock();
            }
        }
    }

    // ---------- Manager hookup ----------
    void TryBindManagerOrWait()
    {
        _mgr = ColorReservationManager.Instance;
        if (_mgr != null) { _mgr.reservations.OnChange += OnReservationsChanged; }
        else { StartCoroutine(WaitForManagerRoutine()); }
    }

    IEnumerator WaitForManagerRoutine()
    {
        float end = Time.unscaledTime + 10f;
        while (_mgr == null && Time.unscaledTime < end)
        {
            _mgr = ColorReservationManager.Instance;
            if (_mgr != null)
            {
                _mgr.reservations.OnChange += OnReservationsChanged;
                RefreshFromReservations(); // if UI already set, reflect state
                yield break;
            }
            yield return null;
        }
        if (_mgr == null) Debug.LogWarning("PlayerColorChooser: Reservation manager not found.");
    }

    // server cleanup
    public override void OnStopServer()
    {
        var mgr = ColorReservationManager.Instance;
        if (mgr != null)
            mgr.ReleaseByOwner(netIdentity.netId);
    }
}
