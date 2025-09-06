using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerPickupInteractor : MonoBehaviour
{
    public enum PickupMode { ClickAndDrag, ClickAndPoint }

    [Header("General")]
    public PickupMode mode = PickupMode.ClickAndDrag;
    public Camera worldCamera;                         // If null, will use Camera.main
    public LayerMask pickupMask = ~0;                  // Which layers are pickable
    public float rayDistance = 100f;                   // Screen->world ray length (for 2D, just needs to exceed scene size)
    public float zWhenOrthographic = 0f;               // Z we’ll place the object at for ortho cameras

    [Header("Click & Drag")]
    public int dragMouseButton = 0;                    // 0 = LMB

    [Header("Click & Point")]
    public int toggleMouseButton = 0;                  // 0 = LMB
    public int cancelMouseButton = 1;                  // 1 = RMB to cancel/drop

    [Header("Quality of Life")]
    [Tooltip("Ignore clicks over UI? (Requires EventSystem/.isPointerOverGameObject check if you have UI)")]
    public bool ignoreUI = false;

    Pickupable _held;
    Vector2 _pickupLocalOffset; // offset from hit point
    float _zDepthCache;

    void Start()
    {
        if (!worldCamera) worldCamera = Camera.main;
        if (worldCamera && !worldCamera.orthographic)
        {
            // For perspective cams, cache initial depth based on camera distance to origin plane
            _zDepthCache = Mathf.Abs(worldCamera.transform.position.z - zWhenOrthographic);
        }
    }

    void Update()
    {
        // Get mouse pos
        Vector2 mouseScreen;
#if ENABLE_INPUT_SYSTEM
        mouseScreen = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
#else
        mouseScreen = Input.mousePosition;
#endif

        if (mode == PickupMode.ClickAndDrag)
        {
            HandleClickAndDrag(mouseScreen);
        }
        else
        {
            HandleClickAndPoint(mouseScreen);
        }

        // While holding, continuously move toward mouse
        if (_held)
        {
            var target = ScreenToWorld(mouseScreen, _held.transform.position.z);
            // Apply the local pick offset so the object stays under the same cursor spot
            target = (Vector2)target + _pickupLocalOffset;
            _held.FollowTo(target, Time.deltaTime);
        }
    }

    void HandleClickAndDrag(Vector2 mouseScreen)
    {
        bool down = GetMouseButtonDown(dragMouseButton);
        bool up = GetMouseButtonUp(dragMouseButton);
        bool held = GetMouseButton(dragMouseButton);

        if (!this._held && down)
        {
            TryPickupUnderCursor(mouseScreen);
        }
        else if (this._held && !held) // button released
        {
            DropHeld();
        }
    }

    void HandleClickAndPoint(Vector2 mouseScreen)
    {
        if (GetMouseButtonDown(cancelMouseButton) && _held)
        {
            DropHeld();
            return;
        }

        if (GetMouseButtonDown(toggleMouseButton))
        {
            if (_held)
            {
                DropHeld();
            }
            else
            {
                TryPickupUnderCursor(mouseScreen);
            }
        }
    }

    void TryPickupUnderCursor(Vector2 mouseScreen)
    {
        if (!worldCamera) return;

        // Optional: ignore UI hits (requires UnityEngine.EventSystems)
        if (ignoreUI && IsPointerOverUI()) return;

        // 2D raycast from cursor
        Vector3 world = ScreenToWorld(mouseScreen, zWhenOrthographic);
        RaycastHit2D hit = Physics2D.Raycast(world, Vector2.zero, 0f, pickupMask);
        if (!hit.collider)
        {
            // If no zero-length hit (rare on some setups), try a small circle cast
            hit = Physics2D.CircleCast(world, 0.05f, Vector2.zero, 0f, pickupMask);
        }

        if (hit.collider && hit.collider.TryGetComponent(out Pickupable p))
        {
            if (!p.CanBePickedBy(transform)) return;

            _held = p;
            _held.OnPickup();

            // Cache offset so the object stays relative to the initial grab point
            Vector2 grabPoint = ScreenToWorld(mouseScreen, _held.transform.position.z);
            _pickupLocalOffset = (Vector2)_held.transform.position - grabPoint;
        }
    }

    void DropHeld()
    {
        if (!_held) return;
        _held.OnDrop();
        _held = null;
        _pickupLocalOffset = Vector2.zero;
    }

    Vector3 ScreenToWorld(Vector2 screen, float targetZ)
    {
        if (!worldCamera) return Vector3.zero;

        if (worldCamera.orthographic)
        {
            var w = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            w.z = targetZ; // keep a fixed Z plane for 2D
            return w;
        }
        else
        {
            // Perspective: project a ray and find a plane at Z=targetZ
            Ray ray = worldCamera.ScreenPointToRay(screen);
            Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, targetZ));
            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }
            // Fallback
            return worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _zDepthCache));
        }
    }

    // ---- Input helpers ----
    bool GetMouseButtonDown(int i)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return Input.GetMouseButtonDown(i);
        return i == 0 ? Mouse.current.leftButton.wasPressedThisFrame
             : i == 1 ? Mouse.current.rightButton.wasPressedThisFrame
             : Mouse.current.middleButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(i);
#endif
    }
    bool GetMouseButtonUp(int i)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return Input.GetMouseButtonUp(i);
        return i == 0 ? Mouse.current.leftButton.wasReleasedThisFrame
             : i == 1 ? Mouse.current.rightButton.wasReleasedThisFrame
             : Mouse.current.middleButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(i);
#endif
    }
    bool GetMouseButton(int i)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return Input.GetMouseButton(i);
        return i == 0 ? Mouse.current.leftButton.isPressed
             : i == 1 ? Mouse.current.rightButton.isPressed
             : Mouse.current.middleButton.isPressed;
#else
        return Input.GetMouseButton(i);
#endif
    }

    // Optional UI guard — only works if you add using UnityEngine.EventSystems;
    bool IsPointerOverUI()
    {
#if UNITY_ENGINE_EVENTSYSTEMS
        return UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
#else
        return false;
#endif
    }
}
