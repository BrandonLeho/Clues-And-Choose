using UnityEngine;

[DisallowMultipleComponent]
public class RouletteBetHoverZones : MonoBehaviour
{
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
    }

    void HideBothPanels()
    {
        if (topPanel) topPanel.Hide();
        if (sidePanel) sidePanel.Hide();
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
    }

    void ForceHidePanelsImmediate()
    {
        if (topPanel) topPanel.ApplyInstantHidden();
        if (sidePanel) sidePanel.ApplyInstantHidden();
    }
}
