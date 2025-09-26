using UnityEngine;

public class CursorControllerModule : MonoBehaviour
{
    public static CursorControllerModule Instance { get; private set; }

    [Header("Textures")]
    [SerializeField] private Texture2D cursorTextureDefault;
    [SerializeField] private Texture2D cursorTextureClickable;
    [SerializeField] private Texture2D cursorTextureDraggable;
    [SerializeField] private Texture2D cursorTextureDragging;

    [Header("Legacy Hotspot")]
    [SerializeField] private Vector2 clickPosition = Vector2.zero;

    [Header("Hotspot Controls")]
    [SerializeField] private bool useNormalizedHotspots = true;
    [Range(0f, 1f)][SerializeField] private Vector2 defaultPivot = new Vector2(0f, 0f);
    [Range(0f, 1f)][SerializeField] private Vector2 clickablePivot = new Vector2(0f, 0f);
    [Range(0f, 1f)][SerializeField] private Vector2 draggablePivot = new Vector2(0f, 0f);
    [Range(0f, 1f)][SerializeField] private Vector2 draggingPivot = new Vector2(0f, 0f);

    [Header("Per-Mode Pixel Nudge")]
    [SerializeField] private Vector2 defaultNudgePx = Vector2.zero;
    [SerializeField] private Vector2 clickableNudgePx = Vector2.zero;
    [SerializeField] private Vector2 draggableNudgePx = Vector2.zero;
    [SerializeField] private Vector2 draggingNudgePx = Vector2.zero;

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

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            var m = _isLocked ? _lockedMode : _currentMode;
            Apply(m);
        }
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

    public void SetPivot(ModeOfCursor mode, Vector2 normalizedPivot, bool applyNow = true)
    {
        normalizedPivot.x = Mathf.Clamp01(normalizedPivot.x);
        normalizedPivot.y = Mathf.Clamp01(normalizedPivot.y);
        switch (mode)
        {
            case ModeOfCursor.Clickable: clickablePivot = normalizedPivot; break;
            case ModeOfCursor.Draggable: draggablePivot = normalizedPivot; break;
            case ModeOfCursor.Dragging: draggingPivot = normalizedPivot; break;
            default: defaultPivot = normalizedPivot; break;
        }
        if (applyNow) Apply(_isLocked ? _lockedMode : _currentMode);
    }

    public void Nudge(ModeOfCursor mode, Vector2 pixels, bool applyNow = true)
    {
        switch (mode)
        {
            case ModeOfCursor.Clickable: clickableNudgePx = pixels; break;
            case ModeOfCursor.Draggable: draggableNudgePx = pixels; break;
            case ModeOfCursor.Dragging: draggingNudgePx = pixels; break;
            default: defaultNudgePx = pixels; break;
        }
        if (applyNow) Apply(_isLocked ? _lockedMode : _currentMode);
    }

    void Apply(ModeOfCursor mode)
    {
        Texture2D tex = null;
        Vector2 hotspot;

        switch (mode)
        {
            case ModeOfCursor.Clickable:
                tex = cursorTextureClickable;
                hotspot = EffectiveHotspot(tex, clickablePivot, clickableNudgePx);
                break;
            case ModeOfCursor.Draggable:
                tex = cursorTextureDraggable;
                hotspot = EffectiveHotspot(tex, draggablePivot, draggableNudgePx);
                break;
            case ModeOfCursor.Dragging:
                tex = cursorTextureDragging;
                hotspot = EffectiveHotspot(tex, draggingPivot, draggingNudgePx);
                break;
            default:
                tex = cursorTextureDefault;
                hotspot = EffectiveHotspot(tex, defaultPivot, defaultNudgePx);
                break;
        }

        Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
    }

    Vector2 EffectiveHotspot(Texture2D tex, Vector2 normalizedPivot, Vector2 nudgePx)
    {
        if (tex == null)
        {
            return clickPosition;
        }

        Vector2 hs = useNormalizedHotspots
            ? new Vector2(Mathf.Round(tex.width * normalizedPivot.x),
                          Mathf.Round(tex.height * normalizedPivot.y))
            : clickPosition;

        hs += nudgePx;

        hs.x = Mathf.Clamp(hs.x, 0, Mathf.Max(0, tex.width - 1));
        hs.y = Mathf.Clamp(hs.y, 0, Mathf.Max(0, tex.height - 1));
        return hs;
    }
}
