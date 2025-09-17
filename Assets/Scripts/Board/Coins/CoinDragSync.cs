using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkCoin))]
public class CoinDragSync : NetworkBehaviour
{
    [Header("Streaming")]
    [SerializeField] float sendInterval = 0.02f;
    [SerializeField] float minSendDelta = 0.0009f;
    [SerializeField] float dragZ = 0f;

    [Header("Smoothing for non-owners")]
    [SerializeField] float lerpSpeed = 20f;
    [SerializeField] float snapIfFar = 0.5f;

    NetworkCoin _coin;
    float _lastSend;
    bool _streaming;

    Vector3 _targetPos;
    bool _hasTarget;

    void Awake()
    {
        _coin = GetComponent<NetworkCoin>();
        _targetPos = transform.position;
    }

    public void BeginLocalDrag()
    {
        if (_coin.IsLocalOwner()) _streaming = true;
    }

    public void EndLocalDrag() => _streaming = false;

    void LateUpdate()
    {
        if (_streaming && _coin.IsLocalOwner())
        {
            if (Time.unscaledTime - _lastSend >= sendInterval)
            {
                _lastSend = Time.unscaledTime;

                var p = transform.position; p.z = dragZ;

                if ((_lastSent - p).sqrMagnitude > minSendDelta)
                {
                    _lastSent = p;
                    CmdMove(p);
                }
            }
            return;
        }

        if (!_coin.IsLocalOwner() && _hasTarget)
        {
            if ((transform.position - _targetPos).sqrMagnitude > snapIfFar * snapIfFar)
            {
                transform.position = _targetPos;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, _targetPos, Time.unscaledDeltaTime * lerpSpeed);
            }
        }
    }

    Vector3 _lastSent;

    [Command(requiresAuthority = false)]
    void CmdMove(Vector3 pos, NetworkConnectionToClient sender = null)
    {
        if (_coin == null || sender?.identity == null) return;
        if (_coin.ownerNetId != sender.identity.netId) return;

        pos.z = dragZ;

        transform.position = pos;

        RpcMoved(pos);
    }

    [ClientRpc(includeOwner = false)]
    void RpcMoved(Vector3 pos)
    {
        pos.z = dragZ;
        _targetPos = pos;
        _hasTarget = true;

        transform.position = Vector3.Lerp(transform.position, _targetPos, 0.25f);
    }
}
