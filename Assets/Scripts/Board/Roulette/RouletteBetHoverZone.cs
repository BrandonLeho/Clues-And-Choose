using UnityEngine;
using System;

[DisallowMultipleComponent]
public class RouletteBetHoverZones : MonoBehaviour
{
    public static bool BetPanelsActive { get; private set; }
    public static event Action<bool> OnBetPanelsActiveChanged;

    void SetBetPanelsActive(bool on)
    {
        if (BetPanelsActive == on) return;
        BetPanelsActive = on;
        OnBetPanelsActiveChanged?.Invoke(on);
    }

    [Header("Panel Animators")]
    [SerializeField] BetPanelPopAnimator topPanel;
    [SerializeField] BetPanelPopAnimator sidePanel;

    [Header("Zones (2D Colliders)")]
    [SerializeField] Collider2D enterZone;
    [SerializeField] Collider2D exitZone;

    [Header("Rules")]
    [SerializeField] bool requireRouletteMode = true;
    [SerializeField] bool requireDraggingCoin = true;

    [Header("Debug")]
    [SerializeField] bool debugLogs = false;

    enum State
    {
        WaitingForEnter,
        TrackingExit
    }

    State _state;

    void Awake()
    {
        ResetToWaitingForEnter();
        ForceHidePanelsImmediate();
        SetBetPanelsActive(false);
    }

    void Update()
    {
        if (!topPanel && !sidePanel) return;

        if (requireRouletteMode && !GameRuleSettings.IsRouletteModeEnabled)
        {
            ForceHideAndReset();
            return;
        }

        var probe = CoinPlacementProbe.Active;
        if (requireDraggingCoin && probe == null)
        {
            ForceHideAndReset();
            return;
        }

        if (probe == null)
        {
            ForceHideAndReset();
            return;
        }

        Vector2 point = (Vector2)probe.GetProbeWorld();

        bool inEnter = enterZone != null && enterZone.enabled && enterZone.OverlapPoint(point);
        bool inExit = exitZone != null && exitZone.enabled && exitZone.OverlapPoint(point);

        if (debugLogs)
        {
            Debug.Log($"[BetZones] state={_state} inEnter={inEnter} inExit={inExit} point={point}");
        }

        switch (_state)
        {
            case State.WaitingForEnter:
                if (inEnter)
                {
                    ShowBothPanels();
                    ArmExitZone();
                }
                break;

            case State.TrackingExit:
                if (!inExit)
                {
                    HideBothPanels();
                    ResetToWaitingForEnter();
                }
                break;
        }
    }

    void ShowBothPanels()
    {
        if (topPanel) topPanel.Show();
        if (sidePanel) sidePanel.Show();
        SetBetPanelsActive(true);
    }

    void HideBothPanels()
    {
        if (topPanel) topPanel.Hide();
        if (sidePanel) sidePanel.Hide();
        SetBetPanelsActive(false);
    }

    void ArmExitZone()
    {
        _state = State.TrackingExit;

        if (enterZone) enterZone.enabled = false;
        if (exitZone) exitZone.enabled = true;
    }

    void ResetToWaitingForEnter()
    {
        _state = State.WaitingForEnter;

        if (enterZone) enterZone.enabled = true;
        if (exitZone) exitZone.enabled = false;
    }

    void ForceHideAndReset()
    {
        HideBothPanels();
        ResetToWaitingForEnter();
        SetBetPanelsActive(false);
    }

    void ForceHidePanelsImmediate()
    {
        if (topPanel) topPanel.ApplyInstantHidden();
        if (sidePanel) sidePanel.ApplyInstantHidden();
        SetBetPanelsActive(false);
    }
}