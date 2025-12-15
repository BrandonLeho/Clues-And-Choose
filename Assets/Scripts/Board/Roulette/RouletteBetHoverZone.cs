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

    bool _panelShown;

    void Awake()
    {
        if (enterZone == null || exitZone == null)
            Debug.LogWarning($"{nameof(RouletteBetHoverZones_UseProbe)} on {name} needs Enter + Exit zones assigned.");
    }

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

        bool inExit = exitZone != null && exitZone.OverlapPoint(point);
        if (inExit)
        {
            if (_panelShown)
            {
                _panelShown = false;
                betPanel.Hide();
            }
            return;
        }

        bool inEnter = enterZone != null && enterZone.OverlapPoint(point);
        if (inEnter)
        {
            if (!_panelShown)
            {
                _panelShown = true;
                betPanel.Show();
            }
        }
    }

    void ForceHide()
    {
        if (_panelShown)
        {
            _panelShown = false;
            betPanel.Hide();
        }
        else
        {
            betPanel.Hide();
        }
    }
}
