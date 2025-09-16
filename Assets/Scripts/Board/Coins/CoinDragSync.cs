using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkCoin))]
public class CoinDragSync : NetworkBehaviour
{
    [SerializeField] float sendInterval = 0.02f;
    [SerializeField] float dragZ = 0f;

    NetworkCoin _coin;
    float _lastSend;
    bool _streaming;

    void Awake() => _coin = GetComponent<NetworkCoin>();

    public void BeginLocalDrag()
    {
        _streaming = true;
        Debug.Log($"[Sync] BeginLocalDrag {name}");
    }

    public void EndLocalDrag()
    {
        _streaming = false;
        Debug.Log($"[Sync] EndLocalDrag {name}");
    }

    void Update()
    {
        if (!_streaming || !_coin.IsLocalOwner()) return;
        if (Time.unscaledTime - _lastSend < sendInterval) return;
        _lastSend = Time.unscaledTime;

        var p = transform.position; p.z = dragZ;
        CmdMove(p);
    }

    [Command(requiresAuthority = false)]
    void CmdMove(Vector3 pos, NetworkConnectionToClient sender = null)
    {
        if (_coin == null || sender?.identity == null) return;

        var senderId = sender.identity.netId;
        if (_coin.ownerNetId != senderId) return;

        Debug.Log($"[Sync][SERVER] apply {name} from {senderId} pos={pos}");
        transform.position = pos;
    }

}
