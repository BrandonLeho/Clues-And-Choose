using System.Collections;
using Mirror;
using UnityEngine;

public class PlayerColorChooser : NetworkBehaviour
{
    [Header("UI (auto-found)")]
    [Tooltip("Optional: if assigned, will be used; otherwise auto-found at runtime.")]
    public SelectionController picker;

    ColorReservationManager _mgr;
    bool _boundPicker;
    bool _subscribedDict;

    void Start()
    {
        if (!isLocalPlayer)  // only the local player drives local UI
            return;

        StartCoroutine(AutoBindRoutine());
    }

    void OnDestroy()
    {
        UnbindPicker();
        UnsubscribeReservations();
    }

    // ---------------- Local → Server ----------------
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
        // SyncDictionary replication will update everyone’s UI.
    }

    [TargetRpc]
    void TargetReservationDenied(NetworkConnectionToClient _, int swatchIndex)
    {
        Debug.Log($"Color {swatchIndex} already taken.");
    }

    [TargetRpc]
    void TargetReservationConfirmed(NetworkConnectionToClient _, int newIndex, int oldIndex)
    {
        // Optional: play a confirm SFX/flash here.
    }

    // ---------------- Replication → UI ----------------
    // Mirror SyncDictionary OnChange: (op, key, item)
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
                s.SetSelected(false);
            }
            else
            {
                if (s.IsLocked) s.Unlock();
            }
        }
    }

    public override void OnStopServer()
    {
        var mgr = ColorReservationManager.Instance;
        if (mgr != null)
            mgr.ReleaseByOwner(netIdentity.netId);
    }

    // ---------------- Binding helpers ----------------
    IEnumerator AutoBindRoutine()
    {
        // Wait for scene & UI to be ready
        while (picker == null)
        {
            // Prefer active-in-hierarchy pickers
            picker = FindActivePicker();
            if (picker != null) break;
            yield return null; // try again next frame
        }

        BindPicker();

        // Also wait for the reservation manager to exist, then subscribe
        while (_mgr == null)
        {
            _mgr = ColorReservationManager.Instance;
            if (_mgr != null) break;
            yield return null;
        }

        SubscribeReservations();
        RefreshFromReservations();
    }

    SelectionController FindActivePicker()
    {
        // Try active objects first
#if UNITY_2023_1_OR_NEWER
        var found = Object.FindFirstObjectByType<SelectionController>();
        if (found != null && found.isActiveAndEnabled && found.gameObject.activeInHierarchy)
            return found;
        // Fallback: find any, including inactive, and prefer one that’s active
        var all = Object.FindObjectsByType<SelectionController>(FindObjectsSortMode.None);
        foreach (var p in all) if (p.isActiveAndEnabled && p.gameObject.activeInHierarchy) return p;
        return all.Length > 0 ? all[0] : null;
#else
        // Older Unity
        var all = Object.FindObjectsOfType<SelectionController>(true); // include inactive
        SelectionController active = null;
        foreach (var p in all) if (p.isActiveAndEnabled && p.gameObject.activeInHierarchy) { active = p; break; }
        return active != null ? active : (all.Length > 0 ? all[0] : null);
#endif
    }

    void BindPicker()
    {
        if (_boundPicker || picker == null) return;
        picker.onColorConfirmed.AddListener(OnLocalConfirm);
        _boundPicker = true;
    }

    void UnbindPicker()
    {
        if (!_boundPicker || picker == null) return;
        picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
        _boundPicker = false;
    }

    void SubscribeReservations()
    {
        if (_subscribedDict || _mgr == null) return;
        _mgr.reservations.OnChange += OnReservationsChanged;
        _subscribedDict = true;
    }

    void UnsubscribeReservations()
    {
        if (!_subscribedDict || _mgr == null) return;
        _mgr.reservations.OnChange -= OnReservationsChanged;
        _subscribedDict = false;
    }
}
