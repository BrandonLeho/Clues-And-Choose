using Mirror;
using UnityEngine;

public class NetPlayerInfo : NetworkBehaviour
{
    [SyncVar] public string playerName;

    public uint NetId => netId;

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = $"Player {netId}";
    }

    private void OnDestroy()
    {
        if (isServer && GameLoopManager.Exists)
            GameLoopManager.Instance.Server_OnPlayerLeft(this);
    }
}
