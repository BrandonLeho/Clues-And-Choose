using UnityEngine;

public class CursorControllerModule : MonoBehaviour
{
    public static CursorControllerModule Instance { get; private set; }

    public enum RenderMode { SystemCursor, WorldSprite }
    public enum ModeOfCursor { Default, Clickable, Draggable, Dragging }

    [Header("General")]
    [SerializeField] private RenderMode renderMode = RenderMode.WorldSprite;

    [Header("System Cursor (Texture2D)")]
    [SerializeField] private Texture2D cursorTextureDefault;
    [SerializeField] private Texture2D cursorTextureClickable;
    [SerializeField] private Texture2D cursorTextureDraggable;
    [SerializeField] private Texture2D cursorTextureDragging;
    [SerializeField] private Vector2 clickPosition = Vector2.zero;

    [Header("World Sprite (SpriteRenderer)")]
    [SerializeField] private Sprite spriteDefault;
    [SerializeField] private Sprite spriteClickable;
    [SerializeField] private Sprite spriteDraggable;
    [SerializeField] private Sprite spriteDragging;

    [Tooltip("Renderer used to draw the cursor in world space. If null, one will be created.")]
    [SerializeField] private SpriteRenderer worldCursorRenderer;

    [Tooltip("Sorting Layer for the world cursor (e.g., 'UI' or a custom overlay layer).")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("Renderer sorting order so the cursor draws on top.")]
    [SerializeField] private int sortingOrder = 5000;

    [Tooltip("Z depth for the world-space cursor. Put it in front of your gameplay plane as needed.")]
    [SerializeField] private float worldZ = 0f;

    [Tooltip("If true, we’ll convert the mouse screen position to world using the main camera each frame.")]
    [SerializeField] private bool followMouse = true;

    Camera _cam;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (renderMode == RenderMode.SystemCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.SetCursor(cursorTextureDefault, clickPosition, CursorMode.Auto);
        }
        else
        {
            // World-sprite cursor
            Cursor.visible = false; // hide OS cursor so only the sprite shows
            Cursor.lockState = CursorLockMode.None;

            if (worldCursorRenderer == null)
            {
                var go = new GameObject("WorldCursor(SR)");
                go.transform.SetParent(null, true);
                worldCursorRenderer = go.AddComponent<SpriteRenderer>();
            }

            worldCursorRenderer.sortingLayerName = sortingLayerName;
            worldCursorRenderer.sortingOrder = sortingOrder;
            worldCursorRenderer.sprite = spriteDefault;

            _cam = Camera.main; // only used for screen→world conversion if followMouse is enabled
        }
    }

    void Update()
    {
        if (renderMode == RenderMode.WorldSprite && worldCursorRenderer != null && followMouse)
        {
            // Convert mouse to world. This uses the camera ONLY for coordinate conversion;
            // the sprite itself renders in world space (no overlays or UI canvases).
            Vector3 sp = Input.mousePosition;
            if (_cam == null) _cam = Camera.main;
            Vector3 wp = (_cam != null) ? _cam.ScreenToWorldPoint(sp) : new Vector3(sp.x, sp.y, 0f);
            wp.z = worldZ;
            worldCursorRenderer.transform.position = wp;
        }
    }

    public void SetToMode(ModeOfCursor modeOfCursor)
    {
        if (renderMode == RenderMode.SystemCursor)
        {
            switch (modeOfCursor)
            {
                case ModeOfCursor.Default: Cursor.SetCursor(cursorTextureDefault, clickPosition, CursorMode.Auto); break;
                case ModeOfCursor.Clickable: Cursor.SetCursor(cursorTextureClickable, clickPosition, CursorMode.Auto); break;
                case ModeOfCursor.Draggable: Cursor.SetCursor(cursorTextureDraggable, clickPosition, CursorMode.Auto); break;
                case ModeOfCursor.Dragging: Cursor.SetCursor(cursorTextureDragging, clickPosition, CursorMode.Auto); break;
                default: Cursor.SetCursor(cursorTextureDefault, clickPosition, CursorMode.Auto); break;
            }
        }
        else if (worldCursorRenderer != null)
        {
            switch (modeOfCursor)
            {
                case ModeOfCursor.Default: worldCursorRenderer.sprite = spriteDefault; break;
                case ModeOfCursor.Clickable: worldCursorRenderer.sprite = spriteClickable; break;
                case ModeOfCursor.Draggable: worldCursorRenderer.sprite = spriteDraggable; break;
                case ModeOfCursor.Dragging: worldCursorRenderer.sprite = spriteDragging; break;
                default: worldCursorRenderer.sprite = spriteDefault; break;
            }
        }
    }
}
