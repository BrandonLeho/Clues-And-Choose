using Mirror;
using UnityEngine;

public class ColorPickerMirrorBinder : NetworkBehaviour
{
    [Header("Refs")]
    public SelectionController picker; // assign from scene if possible

    ColorLockRegistry _registry;

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();

        picker = Object.FindFirstObjectByType<SelectionController>(FindObjectsInactive.Include);
        _registry = ColorLockRegistry.Instance;

        if (!_registry || !picker) return;

        // Run the local UI in network-authoritative mode (don’t lock instantly)
        picker.networkAuthoritative = true;

        // When the local user presses Confirm, ask the server
        picker.onColorConfirmed.AddListener(OnLocalConfirm);

        // Listen for server state changes (including late-join sync)
        _registry.OnRegistryChanged += RefreshFromRegistry;

        // Prime UI
        RefreshFromRegistry();
    }

    void OnDestroy()
    {
        if (isLocalPlayer && picker) picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
        if (_registry != null) _registry.OnRegistryChanged -= RefreshFromRegistry;
    }

    void OnLocalConfirm(Color color, int index)
    {
        // Ask the server to confirm our choice. The UI will reflect the result
        // when the registry changes (success) or remain unchanged (fail).
        CmdTryConfirm(index);
    }

    [Command]
    void CmdTryConfirm(int index, NetworkConnectionToClient sender = null)
    {
        var ok = ColorLockRegistry.Instance && ColorLockRegistry.Instance.TryConfirm(netIdentity, index);
        // optional: if you want immediate client feedback on failure, send a TargetRpc here
        // TargetConfirmResult(sender, ok);
        // Not strictly necessary because the UI only updates on success (registry change).
    }

    void RefreshFromRegistry()
    {
        if (!_registry || !picker) return;

        // 1) Apply global lock/unlock state to each swatch
        for (int i = 0; i < picker.swatches.Count; i++)
        {
            bool isLocked = _registry.lockedBy.ContainsKey(i);
            picker.SetSwatchLockedState(i, isLocked);
        }

        // 2) If I have a lock, make sure my picker internally knows which one is mine
        int myIndex = _registry.FindIndexLockedBy(netIdentity.netId);
        if (myIndex >= 0)
        {
            picker.SetLockedFromNetwork(myIndex); // sets _locked and keeps it visually selected
        }
    }
}
