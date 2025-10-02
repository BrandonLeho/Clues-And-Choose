using Mirror;
using UnityEngine;
using System;

public sealed class GameRuleSettings : NetworkBehaviour
{
    public static GameRuleSettings Instance { get; private set; }

    public static event Action<bool> OnLockAllCoinsChanged;

    [SyncVar(hook = nameof(OnLockAllCoinsChangedHook))]
    public bool lockAllCoinsEnabled;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnLockAllCoinsChangedHook(bool _, bool newValue)
    {
        OnLockAllCoinsChanged?.Invoke(newValue);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetLockAllCoinsEnabled(bool value)
    {
        lockAllCoinsEnabled = value;
    }

    public static bool IsLockAllEnabled => Instance && Instance.lockAllCoinsEnabled;
}
