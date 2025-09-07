using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ScoreboardAttachToBoard : MonoBehaviour
{
    [Header("Links")]
    public Camera targetCamera;
    public SpriteRenderer boardRenderer;
    public RectTransform scoreboardCanvas;

    [Header("Sizing (relative to board)")]
    [Range(0.1f, 1.2f)] public float widthPctOfBoard = 1.0f;
    [Tooltip("Scoreboard height = (scoreboard width) * heightOverWidth")]
    public float heightOverWidth = 0.10f;

    [Header("Placement")]
    [Tooltip("Gap above the board (world units)")]
    public float gapAboveBoard = 0.05f;
    public float zOffset = 0f;

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

        var spriteSize = boardRenderer.sprite.bounds.size;
        var s = boardRenderer.transform.localScale;
        float boardW = spriteSize.x * s.x;
        float boardH = spriteSize.y * s.y;

        float sbWidth = Mathf.Max(0.0001f, boardW * widthPctOfBoard);
        float sbHeight = Mathf.Max(0.0001f, sbWidth * heightOverWidth);

        scoreboardCanvas.sizeDelta = new Vector2(sbWidth, sbHeight);

        Vector3 boardCenter = boardRenderer.bounds.center;
        float topY = boardCenter.y + boardH * 0.5f;
        scoreboardCanvas.pivot = new Vector2(0.5f, 1f);
        scoreboardCanvas.anchorMin = scoreboardCanvas.anchorMax = new Vector2(0.5f, 1f);

        Vector3 pos = new Vector3(boardCenter.x, topY + gapAboveBoard, boardRenderer.transform.position.z + zOffset);
        scoreboardCanvas.position = pos;

        scoreboardCanvas.rotation = Quaternion.identity;
        scoreboardCanvas.localScale = Vector3.one;

        _lastScreen = new Vector2Int(Screen.width, Screen.height);
        _lastOrtho = targetCamera.orthographicSize;
        _lastAspect = targetCamera.aspect;
        _lastBoardScale = boardRenderer.transform.localScale;
        _lastBoardPos = boardRenderer.transform.position;
    }
}
