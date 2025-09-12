using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class CoinDragSync : NetworkBehaviour
{
    [Header("Authority & Owner")]
    [SyncVar] public uint ownerNetId;
    [SerializeField] float remoteLerpSpeed = 18f;

    [Header("State (replicated)")]
    [SyncVar(hook = nameof(OnPosChanged))] Vector2 syncedAnchoredPos;
    [SyncVar] bool syncedDragging;

    RectTransform _rt;
    Canvas _canvas;
    Camera _uiCam;

    Vector2 _displayPos;
    bool _haveDisplay;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _uiCam = _canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_rt)
        {
            _displayPos = _rt.anchoredPosition;
            _haveDisplay = true;
        }
    }

    void Update()
    {
        if (IsLocalOwner()) return;
        if (!_haveDisplay || !_rt) return;

        _displayPos = Vector2.Lerp(_displayPos, syncedAnchoredPos, 1f - Mathf.Exp(-remoteLerpSpeed * Time.unscaledDeltaTime));
        _rt.anchoredPosition = _displayPos;
    }

    bool IsLocalOwner()
    {
        if (!NetworkClient.active) return true;
        var local = NetworkClient.localPlayer;
        return local && local.netId == ownerNetId;
    }

    public void OwnerBeginDrag()
    {
        if (!IsLocalOwner()) return;
        CmdBeginDrag();
    }

    public void OwnerUpdateDrag(Vector2 anchoredPos)
    {
        if (!IsLocalOwner()) return;
        if (_rt) _rt.anchoredPosition = anchoredPos;
        CmdUpdatePos(anchoredPos);
    }

    public void OwnerEndDrag(Vector2 anchoredPos)
    {
        if (!IsLocalOwner()) return;
        CmdEndDrag(anchoredPos);
    }
    bool IsSenderOwner()
    {
        var sender = connectionToClient?.identity;
        return sender && sender.netId == ownerNetId;
    }

    [Command]
    void CmdBeginDrag()
    {
        if (!IsSenderOwner()) return;
        syncedDragging = true;
    }

    [Command]
    void CmdUpdatePos(Vector2 anchoredPos)
    {
        if (!IsSenderOwner()) return;
        syncedAnchoredPos = anchoredPos;
    }

    [Command]
    void CmdEndDrag(Vector2 anchoredPos)
    {
        if (!IsSenderOwner()) return;
        syncedAnchoredPos = anchoredPos;
        syncedDragging = false;
    }

    void OnPosChanged(Vector2 _, Vector2 newPos)
    {
        if (IsLocalOwner()) return;
        if (_rt)
        {
            if (!_haveDisplay) { _displayPos = newPos; _haveDisplay = true; }
        }
    }
}
