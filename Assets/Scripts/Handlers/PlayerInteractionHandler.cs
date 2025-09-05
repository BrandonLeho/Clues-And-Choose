using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

namespace SteamLobbySpace
{
    public class PlayerInteractionHandler : NetworkBehaviour
    {
        [SerializeField] float zDepthFromCamera = 10f;

        [ClientCallback]
        void OnEnable() { if (isLocalPlayer) Cursor.visible = false; }
        [ClientCallback]
        void OnDisable() { if (isLocalPlayer) Cursor.visible = true; }

        [ClientCallback]
        void Update()
        {
            if (!isLocalPlayer) return;

            Vector2 sp = Mouse.current.position.ReadValue();   // New Input System
            var mp = new Vector3(sp.x, sp.y, zDepthFromCamera);
            Vector3 world = Camera.main.ScreenToWorldPoint(mp);
            transform.position = new Vector3(world.x, world.y, 0f);
        }
    }
}
