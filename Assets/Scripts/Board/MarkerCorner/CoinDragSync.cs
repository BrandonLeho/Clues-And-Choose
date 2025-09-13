using Mirror;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class CoinDragSync : NetworkBehaviour
{
    [Header("Ownership")]
    [Tooltip("NetId of the player who is allowed to drive this coin.")]
    public uint ownerNetId;

    [Header("Smoothing (non-owners)")]
    [SerializeField] bool smoothRemoteMotion = true;
    [SerializeField, Range(1f, 50f)] float smoothLerp = 18f;

    [Header("Bandwidth")]
    [Tooltip("Server → clients updates are unreliable to reduce drag latency.")]
    public bool useUnreliableForDrags = true;

    RectTransform _rt;
    Vector2 _targetPos;     // where remotes should lerp to
    bool _dragRemoteVisual; // for remote-only visual state

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        // Non-owners receive target positions via RPCs and optionally smooth.
        if (!isLocalPlayer && isClient)
        {
            if (smoothRemoteMotion)
            {
                float k = 1f - Mathf.Exp(-smoothLerp * Time.unscaledDeltaTime);
                _rt.anchoredPosition = Vector2.Lerp(_rt.anchoredPosition, _targetPos, k);
            }
            else
            {
                _rt.anchoredPosition = _targetPos;
            }
        }
    }

    // ---- Called by DraggableCoin (local owner only) ----

    public void OwnerBeginDrag()
    {
        if (!isClient) return;
        // We deliberately allow calling without authority to avoid warnings
        // on scene objects; server validates the sender.
        CmdBeginDrag();
    }

    public void OwnerUpdateDrag(Vector2 anchoredLocalPos)
    {
        if (!isClient) return;
        CmdUpdatePos(anchoredLocalPos);
    }

    public void OwnerEndDrag(Vector2 anchoredLocalPos)
    {
        if (!isClient) return;
        CmdEndDrag(anchoredLocalPos);
    }

    // ---- Server-side validation helper ----

    bool IsSenderTheOwner(NetworkConnectionToClient sender)
    {
        if (sender == null || sender.identity == null) return false;
        return ownerNetId != 0 && sender.identity.netId == ownerNetId;
    }

    // ---- Commands (server authoritative; no authority required, we validate) ----

    [Command(requiresAuthority = false)]
    void CmdBeginDrag(NetworkConnectionToClient sender = null)
    {
        if (!IsSenderTheOwner(sender)) return;
        RpcBeginDrag();
    }

    [Command(requiresAuthority = false)]
    void CmdUpdatePos(Vector2 anchoredLocalPos, NetworkConnectionToClient sender = null)
    {
        if (!IsSenderTheOwner(sender)) return;

        // Pick channel based on setting
        if (useUnreliableForDrags)
            RpcUpdatePos_Unreliable(anchoredLocalPos);
        else
            RpcUpdatePos(anchoredLocalPos);
    }

    [Command(requiresAuthority = false)]
    void CmdEndDrag(Vector2 anchoredLocalPos, NetworkConnectionToClient sender = null)
    {
        if (!IsSenderTheOwner(sender)) return;
        RpcEndDrag(anchoredLocalPos);
    }

    // ---- Client RPCs (broadcast to everyone else; owner excluded where useful) ----

    [ClientRpc(includeOwner = false)]
    void RpcBeginDrag()
    {
        _dragRemoteVisual = true;
    }

    // Reliable version (optional)
    [ClientRpc(includeOwner = false)]
    void RpcUpdatePos(Vector2 anchoredLocalPos)
    {
        _targetPos = anchoredLocalPos;
        if (!smoothRemoteMotion) _rt.anchoredPosition = anchoredLocalPos;
    }

    // Unreliable, lower latency while dragging
    [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
    void RpcUpdatePos_Unreliable(Vector2 anchoredLocalPos)
    {
        _targetPos = anchoredLocalPos;
        if (!smoothRemoteMotion) _rt.anchoredPosition = anchoredLocalPos;
    }

    [ClientRpc(includeOwner = false)]
    void RpcEndDrag(Vector2 anchoredLocalPos)
    {
        _dragRemoteVisual = false;
        _targetPos = anchoredLocalPos;
        if (!smoothRemoteMotion) _rt.anchoredPosition = anchoredLocalPos;
    }
}
