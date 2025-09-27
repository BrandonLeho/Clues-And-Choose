using Mirror;
using UnityEngine;
using System;

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
    [SerializeField] float minSendInterval = 0.05f;

    [Header("Observer Streaming Detect")]
    [SerializeField] float remoteIdleTimeout = 0.20f;

    float _lastSendTime = -999f;
    NetworkCoin _coin;
    float _lastAutoSend;
    bool _streaming;

    Vector3 _targetPos;
    Vector3 _targetScale;
    bool _hasTarget;

    Vector3 _lastSentPos;
    Vector3 _lastSentScale;

    float _lastRemoteRxTime;
    bool _remoteStreaming;

    public bool IsOwnerStreaming => _streaming && _coin != null && _coin.IsLocalOwner();
    public bool IsRemotelyStreaming => !(_coin != null && _coin.IsLocalOwner()) && _remoteStreaming;

    public event Action<bool> OnStreamStateChanged;

    void Awake()
    {
        _coin = GetComponent<NetworkCoin>();
        _targetPos = transform.position;
        _targetScale = transform.localScale;
        _lastSentPos = _targetPos;
        _lastSentScale = _targetScale;
    }

    public void BeginLocalDrag()
    {
        if (_coin.IsLocalOwner())
        {
            SetOwnerStreaming(true);
        }
    }

    public void EndLocalDrag() => SetOwnerStreaming(false);

    void SetOwnerStreaming(bool v)
    {
        if (_streaming == v) return;
        _streaming = v;
        OnStreamStateChanged?.Invoke(IsOwnerStreaming || IsRemotelyStreaming);
    }

    void LateUpdate()
    {
        if (_coin.IsLocalOwner())
        {
            if (Time.unscaledTime - _lastAutoSend >= sendInterval)
            {
                _lastAutoSend = Time.unscaledTime;

                var p = transform.position;
                p.z = dragZ;

                bool posChanged = (_lastSentPos - p).sqrMagnitude > minSendDelta;
                bool scaleChanged = (_lastSentScale - transform.localScale).sqrMagnitude > minSendDelta;

                if (posChanged || scaleChanged)
                {
                    _lastSentPos = p;
                    _lastSentScale = transform.localScale;
                    CmdMove(p, _lastSentScale);
                }
            }
            return;
        }

        if (_hasTarget)
        {
            if ((transform.position - _targetPos).sqrMagnitude > snapIfFar * snapIfFar)
            {
                transform.position = _targetPos;
                transform.localScale = _targetScale;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, _targetPos, Time.unscaledDeltaTime * lerpSpeed);
                transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * lerpSpeed);
            }
        }

        if (_remoteStreaming && (Time.unscaledTime - _lastRemoteRxTime) > remoteIdleTimeout)
        {
            _remoteStreaming = false;
            OnStreamStateChanged?.Invoke(false);
        }
    }

    [Command(requiresAuthority = false)]
    void CmdMove(Vector3 pos, Vector3 scale, NetworkConnectionToClient sender = null)
    {
        if (_coin == null || sender?.identity == null) return;
        if (_coin.ownerNetId != sender.identity.netId) return;

        pos.z = dragZ;

        transform.position = pos;
        transform.localScale = scale;

        RpcMoved(pos, scale);
    }

    [ClientRpc(includeOwner = false)]
    void RpcMoved(Vector3 pos, Vector3 scale)
    {
        pos.z = dragZ;
        _targetPos = pos;
        _targetScale = scale;
        _hasTarget = true;

        transform.position = Vector3.Lerp(transform.position, _targetPos, 0.25f);
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, 0.25f);

        _lastRemoteRxTime = Time.unscaledTime;
        if (!_remoteStreaming)
        {
            _remoteStreaming = true;
            OnStreamStateChanged?.Invoke(true);
        }
    }

    public void OwnerSnapTo(Vector3 worldPos, Vector3 scale)
    {
        if (!isClient) return;
        if (_coin != null && _coin.IsLocalOwner())
        {
            CmdMove(worldPos, scale);
        }
    }

    public void OwnerSendPositionThrottled(Vector3 worldPos, Vector3 scale)
    {
        if (!isClient) return;
        if (_coin == null || !_coin.IsLocalOwner()) return;

        if (Time.time - _lastSendTime < minSendInterval) return;
        _lastSendTime = Time.time;
        CmdMove(worldPos, scale);
    }
}
