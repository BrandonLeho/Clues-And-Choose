using Mirror;
using UnityEngine;

public class ColorPickerMirrorBinder : NetworkBehaviour
{
    [Header("Refs")]
    public SelectionController picker;

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

        picker.onCancelLockRequested.AddListener(OnCancelLockRequested);

        _registry.OnRegistryChanged += RefreshFromRegistry;
    }

    public override void OnStopAuthority()
    {
        if (picker)
        {
            picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
            picker.onCancelLockRequested.RemoveListener(OnCancelLockRequested);
        }
        if (_registry != null) _registry.OnRegistryChanged -= RefreshFromRegistry;
        base.OnStopAuthority();
    }


    void OnDestroy()
    {
        if (isLocalPlayer && picker) picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
        if (_registry != null) _registry.OnRegistryChanged -= RefreshFromRegistry;
    }

    void OnLocalConfirm(Color color, int index)
    {
        CmdTryConfirm(index, (Color32)color);
    }

    void OnCancelLockRequested()
    {
        CmdUnlockMine();
    }

    [Command]
    void CmdTryConfirm(int index, Color32 color, NetworkConnectionToClient sender = null)
    {
        ColorLockRegistry.Instance?.TryConfirm(netIdentity, index, color);
    }

    [Command]
    void CmdUnlockMine(NetworkConnectionToClient sender = null)
    {
        ColorLockRegistry.Instance?.UnlockAllFor(netIdentity);
    }

    void RefreshFromRegistry()
    {
        if (!_registry || !picker) return;
        if (!picker || !picker.isActiveAndEnabled) return;

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
