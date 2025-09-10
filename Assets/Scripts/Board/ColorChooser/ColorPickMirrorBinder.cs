// ColorPickMirrorBinder.cs (relevant additions)
using Mirror;
using UnityEngine;

public class ColorPickerMirrorBinder : NetworkBehaviour
{
    public SelectionController picker;
    ColorLockRegistry _registry;
    readonly System.Collections.Generic.Dictionary<int, string> _lastLabels = new System.Collections.Generic.Dictionary<int, string>();

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
        _registry.OnRegistryChanged += RefreshFromRegistry;

        picker.onCancelLockRequested.AddListener(OnLocalCancelLock);

    }

    public override void OnStopAuthority()
    {
        if (picker)
        {
            picker.onColorConfirmed.RemoveListener(OnLocalConfirm);
            picker.onCancelLockRequested.RemoveListener(OnLocalCancelLock);
        }
        if (_registry != null) _registry.OnRegistryChanged -= RefreshFromRegistry;
        base.OnStopAuthority();
    }

    void OnLocalConfirm(Color color, int index) => CmdTryConfirm(index, (Color32)color);

    void OnLocalCancelLock() => CmdCancelMyLock();

    [Command]
    void CmdTryConfirm(int index, Color32 color, NetworkConnectionToClient sender = null)
    {
        ColorLockRegistry.Instance?.TryConfirm(netIdentity, index, color);
    }

    [Command]
    void CmdCancelMyLock(NetworkConnectionToClient sender = null)
    {
        ColorLockRegistry.Instance?.UnlockAllFor(netIdentity);
    }

    void RefreshFromRegistry()
    {
        if (!_registry || !picker || !picker.isActiveAndEnabled) return;

        for (int i = 0; i < picker.swatches.Count; i++)
        {
            bool isLocked = _registry.lockedBy.ContainsKey(i);
            picker.SetSwatchLockedState(i, isLocked);

            if (isLocked && _registry.labelByIndex.TryGetValue(i, out string owner))
            {
                if (!_lastLabels.TryGetValue(i, out var prev) || prev != owner)
                {
                    picker.SetOwnerName(i, owner);
                    _lastLabels[i] = owner;
                }
            }
            else
            {
                if (_lastLabels.Remove(i))
                    picker.ClearOwnerName(i);
            }
        }

        int myIndex = _registry.FindIndexLockedByLocal(netIdentity.netId);
        if (myIndex >= 0) picker.SetLockedFromNetwork(myIndex);
    }

}
