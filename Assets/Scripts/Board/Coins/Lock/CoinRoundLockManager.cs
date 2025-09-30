using System.Collections.Generic;
using UnityEngine;

public sealed class CoinRoundLockManager : MonoBehaviour
{
    static CoinRoundLockManager _instance;
    public static CoinRoundLockManager Instance => _instance;

    public static bool IsLocked { get; private set; } = true;
    public static event System.Action OnLocked;
    public static event System.Action OnUnlocked;

    static readonly HashSet<CoinLockedState> _locks = new HashSet<CoinLockedState>();
    static bool _initialLockAnnouncementDone;

    void Awake()
    {
        if (_instance && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    public static void Register(CoinLockedState l)
    {
        if (!l) return;
        _locks.Add(l);

        if (!_initialLockAnnouncementDone && IsLocked)
        {
            _initialLockAnnouncementDone = true;
            OnLocked?.Invoke();
        }
    }

    public static void Unregister(CoinLockedState l)
    {
        if (!l) return;
        _locks.Remove(l);
    }

    [ContextMenu("Unlock All Coins")]
    public void UnlockAllCoins()
    {
        foreach (var l in _locks) if (l) l.Unlock();
        if (IsLocked)
        {
            IsLocked = false;
            OnUnlocked?.Invoke();
        }
    }

    [ContextMenu("Lock All Coins")]
    public void LockAllCoins()
    {
        foreach (var l in _locks) if (l) l.Lock();
        if (!IsLocked)
        {
            IsLocked = true;
            OnLocked?.Invoke();
        }
    }

    public void ReannounceLocked()
    {
        if (IsLocked) OnLocked?.Invoke();
        else OnUnlocked?.Invoke();
    }
}
