using Mirror;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class NetworkCursorWorld : NetworkBehaviour
{
    [SerializeField] float zDepth = 10f;

    void OnDisable()
    {
        if (isLocalPlayer) Cursor.visible = true;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

#if ENABLE_INPUT_SYSTEM
        Vector2 sp = Mouse.current.position.ReadValue();
#else
        Vector2 sp = Input.mousePosition;
#endif

        var world = Camera.main.ScreenToWorldPoint(new Vector3(sp.x, sp.y, zDepth));
        transform.position = new Vector3(world.x, world.y, 0f);
    }
}
