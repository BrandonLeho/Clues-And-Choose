using Mirror;
using UnityEngine;

public class ClueGiverState : NetworkBehaviour
{
    public static ClueGiverState Instance;

    [SyncVar] public uint currentClueGiverNetId;

    void Awake() => Instance = this;

    public static bool IsLocalPlayerClueGiver()
    {
        var me = NetworkClient.connection?.identity;
        return me && Instance && me.netId == Instance.currentClueGiverNetId;
    }

    [Server] public void ServerSetClueGiver(uint netId) => currentClueGiverNetId = netId;
}
