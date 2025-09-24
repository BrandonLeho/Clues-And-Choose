using Mirror;
using UnityEngine;

public class PlayerRoundRegistrant : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (RoundManager.Instance)
            RoundManager.Instance.ServerRegisterPlayer(netId);
    }

    public override void OnStopServer()
    {
        if (RoundManager.Instance)
            RoundManager.Instance.ServerUnregisterPlayer(netId);
        base.OnStopServer();
    }
}
