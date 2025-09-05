using UnityEngine;

public class CursorControllerModule : MonoBehaviour
{
    public static CursorControllerModule Instance { get; private set; }

    [SerializeField] private Texture2D cursorTextureDefault;
    [SerializeField] private Texture2D cursorTextureClickable;
    [SerializeField] private Texture2D cursorTextureDraggable;
    [SerializeField] private Texture2D cursorTextureDragging;

    [SerializeField] private Vector2 clickPosition = Vector2.zero;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Cursor.SetCursor(cursorTextureDefault, clickPosition, CursorMode.Auto);
    }

    public void SetToMode(ModeOfCursor modeOfCursor)
    {
        switch (modeOfCursor)
        {
            case ModeOfCursor.Default:
                Cursor.SetCursor(cursorTextureDefault, clickPosition, CursorMode.Auto);
                break;
            case ModeOfCursor.Clickable:
                Cursor.SetCursor(cursorTextureClickable, clickPosition, CursorMode.Auto);
                break;
            case ModeOfCursor.Draggable:
                Cursor.SetCursor(cursorTextureDraggable, clickPosition, CursorMode.Auto);
                break;
            case ModeOfCursor.Dragging:
                Cursor.SetCursor(cursorTextureDragging, clickPosition, CursorMode.Auto);
                break;
            default:
                Cursor.SetCursor(cursorTextureDefault, clickPosition, CursorMode.Auto);
                break;
        }
    }

    public enum ModeOfCursor
    {
        Default,
        Clickable,
        Draggable,
        Dragging
    }
}