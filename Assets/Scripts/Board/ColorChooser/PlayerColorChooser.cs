using System.Collections;
using Mirror;
using UnityEngine;

public class PlayerColorChooser : NetworkBehaviour
{
    [Header("UI (auto-found if left empty)")]
    public SelectionController picker;
    [Tooltip("Optional: if you know the GameObject name that holds the picker, set it here for a faster lookup.")]
    public string pickerObjectNameOverride = "";
    [Tooltip("How long to keep searching for the UI (unscaled seconds).")]
    public float uiSearchTimeoutSeconds = 10f;
    [Tooltip("Include inactive objects in the search (useful if your UI starts disabled).")]
    public bool includeInactiveInSearch = true;

    ColorReservationManager _mgr;

    void Start()
    {
        // Subscribe to reservation replication on all clients
        TryBindManagerOrWait();

        // Only the local player needs to wire up local UI events
        if (isLocalPlayer)
        {
            // If not assigned in Inspector, auto-find it
            if (!picker) TryFindPickerImmediate();
            if (!picker) StartCoroutine(WaitForPickerRoutine());

            // If already present, bind now
            if (picker) BindPickerEvents();
        }

        // If manager is already present, reflect state into UI (if UI already found)
        if (picker && _mgr) RefreshFromReservations();
    }

    void OnDestroy()
    {
        if (_mgr != null)
            _mgr.reservations.OnChange -= OnReservationsChanged;

        if (isLocalPlayer && picker != null)
            picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
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
    }

    [TargetRpc]
    void TargetReservationDenied(NetworkConnectionToClient _, int swatchIndex)
    {
        Debug.Log($"Color {swatchIndex} is already taken.");
        // Optional: shake button / SFX
    }

    [TargetRpc]
    void TargetReservationConfirmed(NetworkConnectionToClient _, int newIndex, int oldIndex)
    {
        // Optional: confirm SFX / flash
    }

    // ---------------- Replication → UI ----------------

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
                if (!s.IsLocked) s.Lock();   // unselectable for all clients
                s.SetSelected(false);        // make sure it isn’t “pending selected”
            }
            else
            {
                if (s.IsLocked) s.Unlock();
            }
        }
    }

    // ---------------- Helpers ----------------

    void TryBindManagerOrWait()
    {
        _mgr = ColorReservationManager.Instance;
        if (_mgr != null)
        {
            _mgr.reservations.OnChange += OnReservationsChanged;
        }
        else
        {
            StartCoroutine(WaitForManagerRoutine());
        }
    }

    IEnumerator WaitForManagerRoutine()
    {
        float end = Time.unscaledTime + uiSearchTimeoutSeconds;
        while (_mgr == null && Time.unscaledTime < end)
        {
            _mgr = ColorReservationManager.Instance;
            if (_mgr != null)
            {
                _mgr.reservations.OnChange += OnReservationsChanged;
                yield break;
            }
            yield return null;
        }
        if (_mgr == null)
            Debug.LogWarning("PlayerColorChooser: ColorReservationManager not found in time.");
    }

    void TryFindPickerImmediate()
    {
        // 1) If you provided a GameObject name, try that first
        if (!string.IsNullOrWhiteSpace(pickerObjectNameOverride))
        {
            var go = GameObject.Find(pickerObjectNameOverride);
            if (go) picker = go.GetComponentInChildren<SelectionController>(includeInactiveInSearch);
            if (picker) return;
        }

        // 2) Generic search across the scene
#if UNITY_2020_1_OR_NEWER
        picker = Object.FindFirstObjectByType<SelectionController>();
#else
        // Fallback for older Unity without includeInactive overload
        picker = Object.FindObjectOfType<SelectionController>();
        if (!picker && includeInactiveInSearch)
        {
            // Slow path: scan all (rarely needed)
            foreach (var ui in Resources.FindObjectsOfTypeAll<SelectionController>())
            {
                if (ui.gameObject.hideFlags == HideFlags.None) { picker = ui; break; }
            }
        }
#endif
    }

    IEnumerator WaitForPickerRoutine()
    {
        float end = Time.unscaledTime + uiSearchTimeoutSeconds;
        while (picker == null && Time.unscaledTime < end)
        {
            TryFindPickerImmediate();
            if (picker) break;
            yield return null;
        }

        if (!picker)
        {
            Debug.LogWarning("PlayerColorChooser: SelectionController not found in scene.");
            yield break;
        }

        BindPickerEvents();

        // Once bound, if we already have replicated state, reflect it now
        RefreshFromReservations();
    }

    void BindPickerEvents()
    {
        if (!isLocalPlayer || picker == null) return;
        picker.onColorConfirmed.RemoveListener(OnLocalConfirm); // avoid double bind
        picker.onColorConfirmed.AddListener(OnLocalConfirm);
    }

    // Server cleanup for this player's reservation
    public override void OnStopServer()
    {
        var mgr = ColorReservationManager.Instance;
        if (mgr != null)
            mgr.ReleaseByOwner(netIdentity.netId);
    }
}
