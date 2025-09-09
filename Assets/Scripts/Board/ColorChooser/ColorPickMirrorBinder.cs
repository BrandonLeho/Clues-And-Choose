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

        if (!picker)
        {
#if UNITY_2023_1_OR_NEWER
            picker = Object.FindFirstObjectByType<SelectionController>(FindObjectsInactive.Include);
#else
            picker = Object.FindObjectOfType<SelectionController>(true);
#endif
        }

        _registry = ColorLockRegistry.Instance;
        if (!_registry || !picker) return;

        picker.networkAuthoritative = true;

        picker.onColorConfirmed.AddListener(OnLocalConfirm);

        // Subscribe to server state changes; DON'T force an immediate refresh here.
        _registry.OnRegistryChanged += RefreshFromRegistry;
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

        for (int i = 0; i < picker.swatches.Count; i++)
        {
            bool isLocked = _registry.lockedBy.ContainsKey(i);
            picker.SetSwatchLockedState(i, isLocked);

            if (isLocked && _registry.labelByIndex.TryGetValue(i, out string owner))
                picker.SetOwnerName(i, owner);
            else
                picker.ClearOwnerName(i);
        }

        int myIndex = _registry.FindIndexLockedByLocal(netIdentity.netId);
        if (myIndex >= 0) picker.SetLockedFromNetwork(myIndex);
    }
}
