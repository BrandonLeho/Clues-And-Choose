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

    bool _panelShown;

    void Update()
    {
        if (!betPanel) return;

        if (requireRouletteMode && !GameRuleSettings.IsRouletteModeEnabled)
        {
            ForceHide();
            return;
        }

        var probe = CoinPlacementProbe.Active;
        if (requireDraggingCoin && probe == null)
        {
            ForceHide();
            return;
        }
        if (probe == null)
        {
            ForceHide();
            return;
        }

        Vector2 point = (Vector2)probe.GetProbeWorld();

        bool inEnter = enterZone != null && enterZone.OverlapPoint(point);
        bool inExit = exitZone != null && exitZone.OverlapPoint(point);

        if (debugLogs)
            Debug.Log($"[BetZones] inEnter={inEnter} inExit={inExit} shown={_panelShown} point={point}");

        if (inEnter)
        {
            if (!_panelShown)
            {
                _panelShown = true;
                betPanel.Show();
            }
            return;
        }

        if (inExit)
        {
            if (_panelShown)
            {
                _panelShown = false;
                betPanel.Hide();
            }
        }
    }

    void ForceHide()
    {
        if (_panelShown) _panelShown = false;
        betPanel.Hide();
    }
}
