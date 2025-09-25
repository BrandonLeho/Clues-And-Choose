using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ArrowProbeHoverRouter : MonoBehaviour
{
    [Header("UI Raycast")]
    [SerializeField] GraphicRaycaster raycaster;
    [SerializeField] EventSystem eventSystem;
    [SerializeField] bool debugLogs = false;

    [Header("Optional: hard toggle raycasts while dragging")]
    [SerializeField] bool disableGraphicRaycastsWhileDragging = true;
    [SerializeField] RectTransform gridRoot;

    GridCellHoverWithCoords _current;
    List<Graphic> _graphics;
    bool _raycastsDisabled;

    void Awake()
    {
        if (gridRoot)
        {
            _graphics = new List<Graphic>(gridRoot.GetComponentsInChildren<Graphic>(true));
        }
    }

    void Reset()
    {
        if (!raycaster) raycaster = FindFirstObjectByType<GraphicRaycaster>();
        if (!eventSystem) eventSystem = FindFirstObjectByType<EventSystem>();
    }

    void Update()
    {
        var probe = CoinPlacementProbe.Active;

        if (disableGraphicRaycastsWhileDragging && _graphics != null)
        {
            if (probe != null && !_raycastsDisabled)
            {
                foreach (var g in _graphics) g.raycastTarget = false;
                _raycastsDisabled = true;
            }
            else if (probe == null && _raycastsDisabled)
            {
                foreach (var g in _graphics) g.raycastTarget = true;
                _raycastsDisabled = false;
            }
        }

        if (probe == null)
        {
            ClearCurrent();
            return;
        }

        if (!raycaster || !eventSystem)
        {
            if (debugLogs) Debug.LogWarning("[ArrowProbeHoverRouter] Missing raycaster or EventSystem.");
            ClearCurrent();
            return;
        }

        Vector2 screenPos = probe.GetProbeScreenPosition();

        var ped = new PointerEventData(eventSystem) { position = screenPos };
        var results = new List<RaycastResult>();
        raycaster.Raycast(ped, results);

        GridCellHoverWithCoords target = null;
        for (int i = 0; i < results.Count; i++)
        {
            var t = results[i].gameObject.transform;
            target = t.GetComponentInParent<GridCellHoverWithCoords>();
            if (target) break;
        }

        if (target != _current)
        {
            if (debugLogs) Debug.Log($"[ArrowProbeHoverRouter] Hover -> {(target ? target.name : "none")}");
            if (_current) _current.ProbeExit();
            _current = target;
            if (_current) _current.ProbeEnter();
        }
    }

    void ClearCurrent()
    {
        if (_current)
        {
            _current.ProbeExit();
            _current = null;
        }
    }
}
