using Mirror;
using UnityEngine;
using System;

public sealed class GameRuleSettings : NetworkBehaviour
{
    public static GameRuleSettings Instance { get; private set; }

    public static event Action<bool> OnLockAllCoinsChanged;
    public static event Action<float> OnTurnDurationChanged;
    public static event Action<int> OnMaxFullCyclesChanged;
    public static event Action<bool> OnRouletteModeChanged;

    [SyncVar(hook = nameof(OnLockAllCoinsChangedHook))]
    public bool lockAllCoinsEnabled;

    [SyncVar(hook = nameof(OnTurnDurationChangedHook))]
    public float turnDurationSeconds = 15f;

    [SyncVar(hook = nameof(OnMaxFullCyclesChangedHook))]
    public int maxFullCycles = 2;

    [SyncVar(hook = nameof(OnRouletteModeChangedHook))]
    public bool rouletteModeEnabled;

    const float DefaultTurnDurationSeconds = 15f;
    const int DefaultMaxFullCycles = 2;

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

    void OnMaxFullCyclesChangedHook(int _, int newValue)
    {
        OnMaxFullCyclesChanged?.Invoke(newValue);
    }

    void OnRouletteModeChangedHook(bool _, bool newValue)
    {
        OnRouletteModeChanged?.Invoke(newValue);
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

    [Command(requiresAuthority = false)]
    public void CmdSetMaxFullCycles(int value)
    {
        int clamped = Mathf.Clamp(value, 1, 20);
        maxFullCycles = clamped;
    }

    [Command(requiresAuthority = false)]
    public void CmdSetRouletteModeEnabled(bool value)
    {
        rouletteModeEnabled = value;

        if (value && lockAllCoinsEnabled)
        {
            lockAllCoinsEnabled = false;
        }
    }

    public static bool IsLockAllEnabled => Instance && Instance.lockAllCoinsEnabled;

    public static float CurrentTurnDurationSeconds =>
        Instance ? Instance.turnDurationSeconds : DefaultTurnDurationSeconds;

    public static int CurrentMaxFullCycles =>
        Instance ? Mathf.Max(1, Instance.maxFullCycles) : DefaultMaxFullCycles;

    public static bool IsRouletteModeEnabled =>
        Instance && Instance.rouletteModeEnabled;
}
