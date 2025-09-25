using Mirror;
using UnityEngine;

public class CoinPlacedLock : NetworkBehaviour, ICoinDragPermission
{
    [SyncVar] public bool locked;

    public bool CanBeginDrag() => !locked;

    [Server] public void ServerSetLocked(bool value) => locked = value;

    [Command]
    public void CmdSetLocked(bool value)
    {
        locked = value;
    }

    public void Lock()
    {
        if (isServer) locked = true;
        else CmdSetLocked(true);
    }

    public void Unlock()
    {
        if (isServer) locked = false;
        else CmdSetLocked(false);
    }
}
