using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(CoinDragHandler))]
public class CoinCursorBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    CoinDragHandler _drag;
    ICoinDragPermission[] _permGuards;
    NetworkCoin _net;
    CoinRejectionFeedback _reject;

    bool _isPointerOver;
    bool _isDragging;

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        _permGuards = GetComponents<ICoinDragPermission>();
        _net = GetComponent<NetworkCoin>();
        _reject = GetComponent<CoinRejectionFeedback>();

        _drag.onPickUp.AddListener(OnPickedUp);
        _drag.onDrop.AddListener(OnDropped);
    }

    void OnDestroy()
    {
        _drag.onPickUp.RemoveListener(OnPickedUp);
        _drag.onDrop.RemoveListener(OnDropped);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        Debug.Log(_isDragging);
        if (_isDragging) return;
        Debug.Log(CanBeginDragLikeHandler());
        if (CanBeginDragLikeHandler())
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Draggable);
        else
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        if (_isDragging) return;

        CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }

    void OnPickedUp()
    {
        _isDragging = true;
        CursorControllerModule.Instance.LockMode(CursorControllerModule.ModeOfCursor.Dragging, this);
    }

    void OnDropped()
    {
        _isDragging = false;
        CursorControllerModule.Instance.UnlockMode(this);

        if (_isPointerOver && CanBeginDragLikeHandler())
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Draggable);
        else
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }

    bool CanBeginDragLikeHandler()
    {
        if (_reject != null && _reject.IsPlaying) return false;
        if (_net != null && !_net.IsLocalOwner()) return false;

        if (_permGuards != null)
        {
            for (int i = 0; i < _permGuards.Length; i++)
                if (_permGuards[i] != null && !_permGuards[i].CanBeginDrag())
                    return false;
        }
        return true;
    }

    void OnDisable()
    {
        if (CursorControllerModule.Instance != null)
        {
            CursorControllerModule.Instance.UnlockMode(this);
            if (!_isDragging)
                CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
        }
    }
}
