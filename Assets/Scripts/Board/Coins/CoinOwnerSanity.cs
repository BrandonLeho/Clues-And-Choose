using Mirror;
using UnityEngine;

public class CoinOwnerSanity : NetworkBehaviour
{
    public override void OnStartClient()
    {
        base.OnStartClient();
        var coin = GetComponent<NetworkCoin>();
        var lp = NetworkClient.localPlayer;
        var myId = lp ? lp.netId : 0u;
        Debug.Log($"[CoinOwnerSanity] client:{(isServer ? "host" : "client")} localPlayer={myId} coinOwner={coin?.ownerNetId} isLocalOwner={coin?.IsLocalOwner()}");
    }
}
