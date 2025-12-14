using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class RouletteBetHoverZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BetPanelPopAnimator betPanel;
    [SerializeField] Camera worldCamera;

    [Header("Pointer Sampling")]
    [SerializeField] float pointerZ = 0f;

    [Header("Rules")]
    [SerializeField] bool requireRouletteMode = true;
    [SerializeField] bool requireDraggingCoin = true;

    Collider2D _col;
    bool _inside;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (!_col) _col = gameObject.AddComponent<BoxCollider2D>();
        if (!worldCamera) worldCamera = Camera.main;
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
        bool nowInside = _col != null && _col.OverlapPoint(worldPoint);

        if (nowInside != _inside)
        {
            _inside = nowInside;
            if (_inside) betPanel.Show();
            else betPanel.Hide();
        }
    }

    void ForceHide()
    {
        if (_inside)
        {
            _inside = false;
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