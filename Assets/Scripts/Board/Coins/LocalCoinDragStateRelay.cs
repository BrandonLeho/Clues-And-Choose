using UnityEngine;

[RequireComponent(typeof(CoinDragHandler))]
[DisallowMultipleComponent]
public class LocalCoinDragStateRelay : MonoBehaviour
{
    public static int LocalDragCount { get; private set; }
    public static bool IsLocalDraggingAnyCoin => LocalDragCount > 0;

    CoinDragHandler _drag;
    NetworkCoin _netCoin;
    bool _counted;

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        _netCoin = GetComponent<NetworkCoin>();

        if (_drag != null)
        {
            _drag.onPickUp.AddListener(OnPickUp);
            _drag.onDrop.AddListener(OnDrop);
        }
    }

    void OnDestroy()
    {
        if (_drag != null)
        {
            _drag.onPickUp.RemoveListener(OnPickUp);
            _drag.onDrop.RemoveListener(OnDrop);
        }
    }

    void OnDisable()
    {
        if (_counted)
        {
            _counted = false;
            LocalDragCount = Mathf.Max(0, LocalDragCount - 1);
        }
    }

    void OnPickUp()
    {
        if (_netCoin != null && !_netCoin.IsLocalOwner()) return;

        if (!_counted)
        {
            _counted = true;
            LocalDragCount++;
        }
    }

    void OnDrop()
    {
        if (_counted)
        {
            _counted = false;
            LocalDragCount = Mathf.Max(0, LocalDragCount - 1);
        }
    }
}