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

    public void BeginLocalDrag() { if (_coin.IsLocalOwner()) _streaming = true; Debug.Log($"[Sync] BeginLocalDrag on {name}"); }
    public void EndLocalDrag() { _streaming = false; }

    void Update()
    {
        if (!_streaming || !_coin.IsLocalOwner()) return;
        if (Time.unscaledTime - _lastSend < sendInterval) return;
        _lastSend = Time.unscaledTime;

        var p = transform.position;
        p.z = dragZ;
        CmdMove(p);
    }

    [Command(requiresAuthority = false)]
    void CmdMove(Vector3 pos, NetworkConnectionToClient sender = null)
    {
        if (_coin == null || sender?.identity == null) return;
        if (_coin.ownerNetId != sender.identity.netId) return;
        transform.position = pos;

        transform.position = pos;
    }
}
