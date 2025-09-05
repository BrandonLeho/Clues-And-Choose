using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

namespace SteamLobbySpace
{
    public class PlayerInteractionHandler : NetworkBehaviour
    {
        [SerializeField] float zDepth = 10f;

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

        [SerializeField] SpriteRenderer targetRenderer;

        public override void OnStartLocalPlayer()
        {
            if (targetRenderer) targetRenderer.enabled = false;
        }
    }
}
