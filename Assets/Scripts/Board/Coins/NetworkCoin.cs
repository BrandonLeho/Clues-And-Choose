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
    }

    void OnColorChanged(Color32 _, Color32 newColor) => ApplyColor(newColor);

    void ApplyColor(Color32 c)
    {
        if (!_visual) _visual = GetComponent<CoinVisual>();
        if (_visual) _visual.SetBaseColor(c);
    }

    public bool IsLocalOwner()
    {
        var me = NetworkClient.connection?.identity;
        return me && me.netId == ownerNetId;
    }
}
