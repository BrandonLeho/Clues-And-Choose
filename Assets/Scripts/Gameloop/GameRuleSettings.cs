using Mirror;
using UnityEngine;
using System;

public sealed class GameRuleSettings : NetworkBehaviour
{
    public static GameRuleSettings Instance { get; private set; }
    public static event Action<bool> OnLockAllCoinsChanged;
    public static event Action<float> OnTurnDurationChanged;
    [SyncVar(hook = nameof(OnLockAllCoinsChangedHook))]
    public bool lockAllCoinsEnabled;
    [SyncVar(hook = nameof(OnTurnDurationChangedHook))]
    public float turnDurationSeconds = 15f;
    const float DefaultTurnDurationSeconds = 15f;

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

    void OnTurnDurationChangedHook(float _, float newValue)
    {
        OnTurnDurationChanged?.Invoke(newValue);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetLockAllCoinsEnabled(bool value)
    {
        lockAllCoinsEnabled = value;
    }

    [Command(requiresAuthority = false)]
    public void CmdSetTurnDurationSeconds(float value)
    {
        float clamped = Mathf.Clamp(value, 1f, 300f);
        turnDurationSeconds = clamped;
    }

    public static bool IsLockAllEnabled => Instance && Instance.lockAllCoinsEnabled;

    public static float CurrentTurnDurationSeconds =>
        Instance ? Instance.turnDurationSeconds : DefaultTurnDurationSeconds;
}
