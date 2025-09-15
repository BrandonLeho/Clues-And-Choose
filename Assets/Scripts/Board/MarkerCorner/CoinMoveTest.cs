using Mirror;
using UnityEngine;

public class CoinAuthorityMoveTest : NetworkBehaviour
{
    [SerializeField] float speed = 400f; // units/second in world-space UI

    void Update()
    {
        if (!isLocalPlayer) return; // only the owning client drives position

        // Simple owner-only movement to verify sync:
        var input = new Vector2(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)
        );

        if (input.sqrMagnitude > 0f)
        {
            // Use world position for world-space canvas
            transform.position += (Vector3)(input.normalized * speed * Time.deltaTime);
        }
    }
}
