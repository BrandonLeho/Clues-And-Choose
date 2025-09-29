using System.Collections.Generic;
using UnityEngine;

public sealed class CoinRoundLockManager : MonoBehaviour
{
    static CoinRoundLockManager _instance;
    static readonly HashSet<CoinLockedState> _locks = new HashSet<CoinLockedState>();

    public static bool IsLockedGlobally { get; private set; } = true;
    public static event System.Action<bool> onGlobalLockStateChanged;

    public static void Register(CoinLockedState l) { if (l) _locks.Add(l); }
    public static void Unregister(CoinLockedState l) { if (l) _locks.Remove(l); }

    void Awake()
    {
        if (_instance && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        SetGlobal(true, invokeEvenIfSame: false);
    }

    [ContextMenu("Unlock All Coins")]
    public void UnlockAllCoins()
    {
        foreach (var l in _locks) if (l) l.Unlock();
        SetGlobal(false);
    }

    [ContextMenu("Lock All Coins")]
    public void LockAllCoins()
    {
        foreach (var l in _locks) if (l) l.Lock();
        SetGlobal(true);
    }

    void SetGlobal(bool locked, bool invokeEvenIfSame = true)
    {
        if (IsLockedGlobally == locked && !invokeEvenIfSame) return;
        IsLockedGlobally = locked;
        onGlobalLockStateChanged?.Invoke(locked);
    }
}
