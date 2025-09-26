using UnityEngine;

public class CursorControllerModule : MonoBehaviour
{
    public static CursorControllerModule Instance { get; private set; }

    [SerializeField] private Texture2D cursorTextureDefault;
    [SerializeField] private Texture2D cursorTextureClickable;
    [SerializeField] private Texture2D cursorTextureDraggable;
    [SerializeField] private Texture2D cursorTextureDragging;

    [SerializeField] private Vector2 clickPosition = Vector2.zero;

    public enum ModeOfCursor { Default, Clickable, Draggable, Dragging }

    ModeOfCursor _currentMode = ModeOfCursor.Default;

    bool _isLocked;
    ModeOfCursor _lockedMode;
    Object _lockOwner;

    public bool IsLocked => _isLocked;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
        Apply(_currentMode);
    }

    public void SetToMode(ModeOfCursor mode)
    {
        _currentMode = mode;
        if (_isLocked) return;
        Apply(mode);
    }

    public void LockMode(ModeOfCursor mode, Object owner = null)
    {
        _isLocked = true;
        _lockedMode = mode;
        _lockOwner = owner;
        Apply(mode);
    }

    public void UnlockMode(Object owner = null)
    {
        if (!_isLocked) return;
        if (owner != null && owner != _lockOwner) return;
        _isLocked = false;
        _lockOwner = null;
        Apply(_currentMode);
    }

    void Apply(ModeOfCursor mode)
    {
        switch (mode)
        {
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
}
