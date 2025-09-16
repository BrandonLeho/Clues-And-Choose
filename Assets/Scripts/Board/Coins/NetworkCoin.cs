using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class NetworkCoin : NetworkBehaviour
{
    [SyncVar] public uint ownerNetId;
    [SyncVar(hook = nameof(OnColorChanged))] public Color32 netColor;

    CoinVisual _visual;

    void Awake() => _visual = GetComponent<CoinVisual>();

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyColor(netColor);

        var lp = NetworkClient.localPlayer;
        var myId = lp ? lp.netId : 0u;
        Debug.Log($"[NetworkCoin] OnStartClient coin({netId}) owner={ownerNetId} localPlayer={myId} isLocalOwner={IsLocalOwner()}");
    }

    void OnColorChanged(Color32 _, Color32 nc) => ApplyColor(nc);

    void ApplyColor(Color32 c)
    {
        if (!_visual) _visual = GetComponent<CoinVisual>();
        if (_visual) _visual.SetBaseColor(c);
    }

    public bool IsLocalOwner()
    {
        var lp = NetworkClient.localPlayer;
        if (!lp) return false;
        return lp.netId == ownerNetId;
    }
}
