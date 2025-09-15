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

    public void BeginLocalDrag() { if (_coin.IsLocalOwner()) _streaming = true; }
    public void EndLocalDrag() { _streaming = false; }

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
        if (_coin && sender != null)
        {
            var owner = _coin.ownerNetId;
            var senderId = sender.identity ? sender.identity.netId : 0u;
            if (owner != 0 && senderId != owner) return;
        }

        transform.position = pos;
    }
}
