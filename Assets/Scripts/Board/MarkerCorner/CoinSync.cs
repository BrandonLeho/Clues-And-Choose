using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableNetUI : NetworkBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Optional: leave blank on prefab")]
    [SerializeField] RectTransform dragTarget;
    [SerializeField] Canvas worldSpaceCanvas;
    [SerializeField] string preferredCanvasTag = "";

    [Header("Tuning")]
    [SerializeField] new float syncInterval = 0.02f;

    Camera _cam;

    void Awake()
    {
        if (!dragTarget) dragTarget = (RectTransform)transform;
        EnsureCanvasAndCamera();
    }

    void OnEnable()
    {
        if (!worldSpaceCanvas || !_cam) EnsureCanvasAndCamera();
    }

    void EnsureCanvasAndCamera()
    {
        if (!string.IsNullOrEmpty(preferredCanvasTag))
        {
            var tagged = GameObject.FindGameObjectWithTag(preferredCanvasTag);
            if (tagged)
            {
                var c = tagged.GetComponent<Canvas>();
                if (c && c.renderMode == RenderMode.WorldSpace) worldSpaceCanvas = c;
            }
        }

        if (!worldSpaceCanvas)
        {
            var pCanvas = GetComponentInParent<Canvas>(true);
            if (pCanvas && pCanvas.renderMode == RenderMode.WorldSpace) worldSpaceCanvas = pCanvas;
        }

#if UNITY_2022_2_OR_NEWER
        if (!worldSpaceCanvas)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            float best = float.PositiveInfinity;
            foreach (var c in canvases)
            {
                if (c && c.renderMode == RenderMode.WorldSpace && c.gameObject.activeInHierarchy)
                {
                    float d = Vector3.SqrMagnitude(c.transform.position - transform.position);
                    if (d < best) { best = d; worldSpaceCanvas = c; }
                }
            }
        }
#else
        if (!worldSpaceCanvas)
        {
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            float best = float.PositiveInfinity;
            foreach (var c in canvases)
            {
                if (c && c.renderMode == RenderMode.WorldSpace && c.gameObject.scene.IsValid())
                {
                    float d = Vector3.SqrMagnitude(c.transform.position - transform.position);
                    if (d < best) { best = d; worldSpaceCanvas = c; }
                }
            }
        }
#endif

        _cam = (worldSpaceCanvas && worldSpaceCanvas.worldCamera) ? worldSpaceCanvas.worldCamera : Camera.main;

        if (!worldSpaceCanvas)
            Debug.LogWarning($"[{nameof(DraggableNetUI)}] No World Space Canvas found. Drag may behave oddly.");

        if (!_cam)
            Debug.LogWarning($"[{nameof(DraggableNetUI)}] No camera found (Canvas.worldCamera and Camera.main are null).");
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!isLocalPlayer) return;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!isLocalPlayer) return;

        var target = dragTarget ? dragTarget : (RectTransform)transform;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(target, e.position, _cam, out var worldPos))
        {
            target.position = worldPos;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!isLocalPlayer) return;
    }
}
