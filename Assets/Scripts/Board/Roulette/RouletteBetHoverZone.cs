using UnityEngine;

[DisallowMultipleComponent]
public class RouletteBetHoverZones_UseProbe : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BetPanelPopAnimator betPanel;

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
    }

    void Update()
    {
        if (!betPanel) return;

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
            Debug.Log($"[BetZones] state={_state} inEnter={inEnter} inExit={inExit} point={point}");

        switch (_state)
        {
            case State.WaitingForEnter:
                {
                    if (inEnter)
                    {
                        betPanel.Show();
                        ArmExitZone();
                    }
                    break;
                }

            case State.TrackingExit:
                {
                    if (!inExit)
                    {
                        betPanel.Hide();
                        ResetToWaitingForEnter();
                    }
                    break;
                }
        }
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
        betPanel.Hide();
        ResetToWaitingForEnter();
    }
}
