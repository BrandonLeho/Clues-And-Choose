using UnityEngine;

/// Fits a SpriteRenderer (your color board) inside the camera view,
/// keeps it centered, and supports an offset from screen center.
/// Works in Edit & Play mode.
[ExecuteAlways]
public class BoardScreenFitter2D : MonoBehaviour
{
    public Camera targetCamera;                 // null -> Camera.main
    public SpriteRenderer boardRenderer;

    [Header("Fit")]
    [Range(0f, 0.45f)] public float marginPct = 0.04f; // 4% breathing room
    public bool fitInside = true;               // true: show whole board; false: fill & crop
    public float boardZ = 0f;                   // z-position for the board

    [Header("Offset from screen center")]
    public OffsetMode offsetMode = OffsetMode.ScreenPixels;
    public Vector2 offset = Vector2.zero;       // meaning depends on mode (see enum below)

    public enum OffsetMode
    {
        WorldUnits,        // offset.x, offset.y are world-space units
        ScreenPixels,      // offset.x, offset.y are pixels relative to screen center
        ViewportPercent    // 1.0 = full view width/height, 0.1 = 10% of view size
    }

    Vector2Int _lastScreenSize;
    float _lastOrtho, _lastAspect;

    void OnEnable()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!boardRenderer) boardRenderer = GetComponent<SpriteRenderer>();
        ApplyFit();
    }

    void Update()
    {
        if (!targetCamera || !boardRenderer || !boardRenderer.sprite) return;

        // Refit on size/aspect/ortho changes (works in Edit mode too)
        var sz = new Vector2Int(Screen.width, Screen.height);
        bool changed = sz != _lastScreenSize ||
                       Mathf.Abs(targetCamera.orthographicSize - _lastOrtho) > 0.0001f ||
                       Mathf.Abs(targetCamera.aspect - _lastAspect) > 0.0001f;

        if (changed) ApplyFit();
    }

    void ApplyFit()
    {
        if (!targetCamera || !boardRenderer || !boardRenderer.sprite) return;

        // Camera visible size in world units (orthographic)
        float viewH = targetCamera.orthographicSize * 2f;
        float viewW = viewH * targetCamera.aspect;

        // Margins
        float availW = Mathf.Max(0.0001f, viewW * (1f - 2f * marginPct));
        float availH = Mathf.Max(0.0001f, viewH * (1f - 2f * marginPct));

        // Current board world size
        var spriteWH = boardRenderer.sprite.bounds.size; // in local units (respects PPU)
        var curScale = boardRenderer.transform.localScale;
        var boardSizeWorldNow = new Vector2(spriteWH.x * curScale.x,
                                            spriteWH.y * curScale.y);

        // Uniform scale multiplier to fit/fill
        float sx = availW / boardSizeWorldNow.x;
        float sy = availH / boardSizeWorldNow.y;
        float mul = fitInside ? Mathf.Min(sx, sy) : Mathf.Max(sx, sy);

        // Apply uniform scaling (preserve non-uniform ratio if any)
        boardRenderer.transform.localScale = new Vector3(
            curScale.x * mul,
            curScale.y * mul,
            curScale.z
        );

        // Recompute board size after scaling (for accurate offset in viewport mode)
        var newScale = boardRenderer.transform.localScale;
        var boardSizeWorld = new Vector2(spriteWH.x * newScale.x,
                                         spriteWH.y * newScale.y);

        // Base position: camera center (x,y) + z
        Vector3 camPos = targetCamera.transform.position;
        Vector3 pos = new Vector3(camPos.x, camPos.y, boardZ);

        // Compute world-space offset from requested mode
        Vector2 worldOffset = Vector2.zero;
        switch (offsetMode)
        {
            case OffsetMode.WorldUnits:
                worldOffset = offset;
                break;

            case OffsetMode.ScreenPixels:
                {
                    // pixels -> world units
                    // One pixel in world = viewW/Screen.width horizontally, viewH/Screen.height vertically
                    float pxToWorldX = viewW / Mathf.Max(1, Screen.width);
                    float pxToWorldY = viewH / Mathf.Max(1, Screen.height);
                    worldOffset = new Vector2(offset.x * pxToWorldX, offset.y * pxToWorldY);
                    break;
                }

            case OffsetMode.ViewportPercent:
                {
                    // 1.0 = full view size; 0.5 = half; 0.1 = 10% of view dimension
                    worldOffset = new Vector2(offset.x * viewW, offset.y * viewH);
                    break;
                }
        }

        // Respect camera rotation (rare in 2D, but this makes it robust)
        Vector3 camRight = targetCamera.transform.right; // world X in camera plane
        Vector3 camUp = targetCamera.transform.up;    // world Y in camera plane
        pos += camRight * worldOffset.x + camUp * worldOffset.y;

        boardRenderer.transform.position = pos;

        // Cache to detect changes
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _lastOrtho = targetCamera.orthographicSize;
        _lastAspect = targetCamera.aspect;
    }
}
