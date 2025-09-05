using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

namespace SteamLobbySpace
{
    public class PlayerInteractionHandler : NetworkBehaviour
    {
        [SerializeField] float zDepth = 10f;

        void OnEnable() { if (isLocalPlayer) Cursor.visible = false; }
        void OnDisable() { if (isLocalPlayer) Cursor.visible = true; }

        void Update()
        {
            if (!isLocalPlayer) return;

#if ENABLE_INPUT_SYSTEM
            Vector2 sp = Mouse.current.position.ReadValue();
#else
            Vector2 sp = Input.mousePosition;
#endif

            Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(sp.x, sp.y, zDepth));
            transform.position = new Vector3(world.x, world.y, 0f);
        }
    }
}
