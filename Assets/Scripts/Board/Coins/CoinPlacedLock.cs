using Mirror;
using UnityEngine;

public class CoinPlacedLock : NetworkBehaviour, ICoinDragPermission
{
    [SyncVar] public bool locked;

    public Transform liftTarget;

    public bool CanBeginDrag() => !locked;
    public void Lock() => locked = true;
    public void Unlock() => locked = false;

    public void SetLocked(bool v)
    {
        if (isServer) locked = v;
        else CmdSetLocked(v);
    }

    [Command(requiresAuthority = false)]
    void CmdSetLocked(bool v) => locked = v;
}
