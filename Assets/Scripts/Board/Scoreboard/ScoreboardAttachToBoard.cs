using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ScoreboardAttachToBoard : MonoBehaviour
{
    [Header("Links")]
    public Camera targetCamera;             // null -> Camera.main
    public SpriteRenderer boardRenderer;    // your color board (fitted by BoardScreenFitter2D)
    public RectTransform scoreboardCanvas;  // the world-space canvas RectTransform (Scoreboard_Canvas)

    [Header("Sizing (relative to board)")]
    [Range(0.1f, 1.2f)] public float widthPctOfBoard = 1.0f;   // 1.0 = same width as board
    [Tooltip("Scoreboard height = (scoreboard width) * heightOverWidth")]
    public float heightOverWidth = 0.10f;                       // e.g., 10% of its own width

    [Header("Placement")]
    [Tooltip("Gap above the board (world units)")]
    public float gapAboveBoard = 0.05f;         // space between board top and scoreboard bottom
    public float zOffset = 0f;                  // push forward/back if needed

    // internal cache
    Vector2Int _lastScreen;
    float _lastOrtho;
    float _lastAspect;
    Vector3 _lastBoardScale;
    Vector3 _lastBoardPos;

    void OnEnable()
    {
        if (!targetCamera) targetCamera = Camera.main;
        Apply();
    }

    void LateUpdate()
    {
        if (!targetCamera || !boardRenderer || !boardRenderer.sprite || !scoreboardCanvas) return;

        var screen = new Vector2Int(Screen.width, Screen.height);
        bool sizeChanged = screen != _lastScreen ||
                           Mathf.Abs(targetCamera.orthographicSize - _lastOrtho) > 0.0001f ||
                           Mathf.Abs(targetCamera.aspect - _lastAspect) > 0.0001f;

        bool boardChanged = _lastBoardScale != boardRenderer.transform.localScale ||
                            _lastBoardPos != boardRenderer.transform.position;

        if (sizeChanged || boardChanged)
            Apply();
    }

    void Apply()
    {
        if (!targetCamera || !boardRenderer || !boardRenderer.sprite || !scoreboardCanvas) return;

        // Board world size after fit
        var spriteSize = boardRenderer.sprite.bounds.size;   // local units
        var s = boardRenderer.transform.localScale;
        float boardW = spriteSize.x * s.x;
        float boardH = spriteSize.y * s.y;

        // Scoreboard world size (RectTransform.sizeDelta is in canvas local units for World Space;
        // we use sizeDelta as the world size here so 1 unit == 1 world unit)
        float sbWidth = Mathf.Max(0.0001f, boardW * widthPctOfBoard);
        float sbHeight = Mathf.Max(0.0001f, sbWidth * heightOverWidth);

        scoreboardCanvas.sizeDelta = new Vector2(sbWidth, sbHeight);

        // Place at top-center of the board, + gap
        // Board center in world:
        Vector3 boardCenter = boardRenderer.bounds.center;
        // Top edge world Y = centerY + boardH/2
        float topY = boardCenter.y + boardH * 0.5f;
        // Scoreboard pivot assumed (0.5,1.0). Set it once:
        scoreboardCanvas.pivot = new Vector2(0.5f, 1f);
        scoreboardCanvas.anchorMin = scoreboardCanvas.anchorMax = new Vector2(0.5f, 1f);

        // World position for scoreboard top-center
        Vector3 pos = new Vector3(boardCenter.x, topY + gapAboveBoard, boardRenderer.transform.position.z + zOffset);
        scoreboardCanvas.position = pos;

        // Face like the board (no rotation changes needed for typical 2D), but keep identity to avoid skew
        scoreboardCanvas.rotation = Quaternion.identity;
        scoreboardCanvas.localScale = Vector3.one;

        // cache
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
        _lastOrtho = targetCamera.orthographicSize;
        _lastAspect = targetCamera.aspect;
        _lastBoardScale = boardRenderer.transform.localScale;
        _lastBoardPos = boardRenderer.transform.position;
    }
}
