using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ArrowProbeHoverRouter : MonoBehaviour
{
    [Header("Scope")]
    [SerializeField] RectTransform gridRoot;
    [SerializeField] bool includeInactive = false;

    [Header("Cameras / Systems")]
    [SerializeField] Canvas gridCanvas;
    [SerializeField] EventSystem eventSystem;

    [Header("Debug")]
    [SerializeField] bool debugLogs = false;

    readonly List<GridCellHoverWithCoords> _cells = new();
    GridCellHoverWithCoords _current;
    Camera _uiCam;

    void Reset()
    {
        if (!gridCanvas) gridCanvas = GetComponentInParent<Canvas>();
        if (!eventSystem) eventSystem = FindFirstObjectByType<EventSystem>();
        if (!gridRoot && gridCanvas) gridRoot = gridCanvas.transform as RectTransform;
    }

    void Awake()
    {
        if (!gridCanvas) gridCanvas = GetComponentInParent<Canvas>();
        if (!eventSystem) eventSystem = FindFirstObjectByType<EventSystem>();
        if (!gridRoot && gridCanvas) gridRoot = gridCanvas.transform as RectTransform;

        _uiCam = (gridCanvas && gridCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? gridCanvas.worldCamera
            : null;

        RebuildCellList();
    }

    public void RebuildCellList()
    {
        _cells.Clear();
        if (!gridRoot) return;
        var tmp = gridRoot.GetComponentsInChildren<GridCellHoverWithCoords>(includeInactive);
        _cells.AddRange(tmp);
        _cells.Sort((a, b) =>
        {
            int ia = a.transform.GetSiblingIndex();
            int ib = b.transform.GetSiblingIndex();
            return ib.CompareTo(ia);
        });
    }

    void Update()
    {
        var probe = CoinPlacementProbe.Active;
        if (probe == null)
        {
            ClearCurrent();
            return;
        }

        Vector2 screenPos = probe.GetProbeScreenPosition();

        GridCellHoverWithCoords target = null;

        for (int i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            if (!cell || !cell.isActiveAndEnabled) continue;

            var rt = cell.transform as RectTransform;
            if (!rt) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, _uiCam))
            {
                target = cell;
                break;
            }
        }

        if (target != _current)
        {
            if (debugLogs) Debug.Log($"[ArrowProbeHoverRouter] Hover -> {(target ? target.name : "none")}");
            if (_current) _current.ProbeExit();
            _current = target;
            if (_current) _current.ProbeEnterAtScreen(screenPos, eventSystem);
        }
        else if (_current)
        {
            _current.ProbeEnterAtScreen(screenPos, eventSystem);
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
