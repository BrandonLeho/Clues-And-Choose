using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CoinPlacementProbe))]
public class CoinProbeUIHoverRouter : MonoBehaviour
{
    [Header("UI Raycast")]
    public GraphicRaycaster uiRaycaster;
    public Camera uiCamera;

    [Header("Update")]
    public bool onlyWhileDragging = true;

    CoinPlacementProbe _probe;
    GridCellHoverWithCoords _currentCell;
    PointerEventData _ped;
    List<RaycastResult> _hits = new List<RaycastResult>();
    CoinDragHandler _drag;

    void Awake()
    {
        _probe = GetComponent<CoinPlacementProbe>();
        _drag = GetComponent<CoinDragHandler>();
        if (!uiCamera) uiCamera = Camera.main;
        _ped = new PointerEventData(EventSystem.current);
        if (_drag)
        {
            _drag.onPickUp.AddListener(() => _active = true);
            _drag.onDrop.AddListener(() => { _active = false; ForceExit(); });
        }
    }

    bool _active = true;

    void Update()
    {
        if (!uiRaycaster) return;
        if (onlyWhileDragging && !_active) return;

        Vector3 probeWorld = _probe ? _probe.GetProbeWorld() : transform.position;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, probeWorld);

        _ped.position = screen;
        _hits.Clear();
        uiRaycaster.Raycast(_ped, _hits);

        GridCellHoverWithCoords found = null;
        for (int i = 0; i < _hits.Count; i++)
        {
            var go = _hits[i].gameObject;
            if (!go) continue;
            found = go.GetComponentInParent<GridCellHoverWithCoords>();
            if (found) break;
        }

        if (found != _currentCell)
        {
            if (_currentCell) _currentCell.ProbeExit();
            _currentCell = found;
            if (_currentCell) _currentCell.ProbeEnter();
        }
    }

    void ForceExit()
    {
        if (_currentCell)
        {
            _currentCell.ProbeExit();
            _currentCell = null;
        }
    }
}
