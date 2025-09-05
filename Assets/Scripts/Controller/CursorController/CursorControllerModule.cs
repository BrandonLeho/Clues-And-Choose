using UnityEngine;

public class CursorControllerModule : MonoBehaviour
{
    public static CursorControllerModule Instance { get; private set; }

    [Header("Cursor Visual (SpriteRenderer-driven)")]
    [SerializeField] private ChangeCursor cursorVisual;   // assign your CursorVisual2D object here

    [Header("Sprites per Mode")]
    [SerializeField] private Sprite spriteDefault;
    [SerializeField] private Sprite spriteClickable;
    [SerializeField] private Sprite spriteDraggable;
    [SerializeField] private Sprite spriteDragging;

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

    private void Start()
    {
        // Ensure our visual exists and default sprite is applied
        if (cursorVisual)
            cursorVisual.SetSprite(spriteDefault);

        // Make sure the hardware cursor is hidden, since we render our own sprite
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
    }

    public void SetToMode(ModeOfCursor modeOfCursor)
    {
        if (!cursorVisual) return;

        switch (modeOfCursor)
        {
            case ModeOfCursor.Default:
                cursorVisual.SetSprite(spriteDefault);
                break;
            case ModeOfCursor.Clickable:
                cursorVisual.SetSprite(spriteClickable);
                break;
            case ModeOfCursor.Draggable:
                cursorVisual.SetSprite(spriteDraggable);
                break;
            case ModeOfCursor.Dragging:
                cursorVisual.SetSprite(spriteDragging);
                break;
            default:
                cursorVisual.SetSprite(spriteDefault);
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
