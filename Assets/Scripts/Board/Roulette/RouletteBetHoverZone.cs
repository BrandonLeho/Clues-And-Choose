using UnityEngine;

[DisallowMultipleComponent]
public class RouletteBetHoverZones2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BetPanelPopAnimator betPanel;
    [SerializeField] Camera worldCamera;

    [Header("Zones (2D Colliders)")]
    [SerializeField] Collider2D enterZone;
    [SerializeField] Collider2D exitZone;

    [Header("Pointer Sampling")]
    [SerializeField] float pointerZ = 0f;

    [Header("Rules")]
    [SerializeField] bool requireRouletteMode = true;
    [SerializeField] bool requireDraggingCoin = true;

    bool _panelShown;

    void Awake()
    {
        if (!worldCamera) worldCamera = Camera.main;
        if (enterZone == null || exitZone == null)
            Debug.LogWarning($"{nameof(RouletteBetHoverZones2D)} on {name} needs both Enter + Exit zones assigned.");
    }

    void Update()
    {
        if (!betPanel) return;

        if (requireRouletteMode && !GameRuleSettings.IsRouletteModeEnabled)
        {
            ForceHide();
            return;
        }

        if (requireDraggingCoin && !LocalCoinDragStateRelay.IsLocalDraggingAnyCoin)
        {
            ForceHide();
            return;
        }

        Vector2 worldPoint = GetPointerWorld2D();

        bool inExit = exitZone != null && exitZone.OverlapPoint(worldPoint);
        if (inExit)
        {
            if (_panelShown)
            {
                _panelShown = false;
                betPanel.Hide();
            }
            return;
        }

        bool inEnter = enterZone != null && enterZone.OverlapPoint(worldPoint);
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

    Vector2 GetPointerWorld2D()
    {
        if (!worldCamera) worldCamera = Camera.main;

        Vector3 sp = Input.mousePosition;
        sp.z = Mathf.Abs(worldCamera.transform.position.z - pointerZ);

        Vector3 wp = worldCamera.ScreenToWorldPoint(sp);
        return new Vector2(wp.x, wp.y);
    }
}
