using System.Collections.Generic;
using UnityEngine;

public sealed class CoinRoundLockManager : MonoBehaviour
{
    static CoinRoundLockManager _instance;
    readonly static HashSet<CoinLockedState> _locks = new HashSet<CoinLockedState>();

    public static void Register(CoinLockedState l) { if (l) _locks.Add(l); }
    public static void Unregister(CoinLockedState l) { if (l) _locks.Remove(l); }

    void Awake()
    {
        if (_instance && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    [ContextMenu("Unlock All Coins")]
    public void UnlockAllCoins()
    {
        foreach (var l in _locks) if (l) l.Unlock();
    }

    [ContextMenu("Lock All Coins")]
    public void LockAllCoins()
    {
        foreach (var l in _locks) if (l) l.Lock();
    }
}
