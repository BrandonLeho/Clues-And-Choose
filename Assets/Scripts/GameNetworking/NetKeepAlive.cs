using Mirror;
using UnityEngine;

public struct KeepAlive : NetworkMessage { }

public class NetKeepAlive : MonoBehaviour
{
    const float interval = 1.0f;
    float t;

    void Awake()
    {
        NetworkClient.RegisterHandler<KeepAlive>(_ => { });
        if (NetworkServer.active)
            NetworkServer.RegisterHandler<KeepAlive>((conn, msg) => { });
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t < interval) return;
        t = 0f;

        if (NetworkClient.active)
            NetworkClient.Send(new KeepAlive());

        if (NetworkServer.active)
            NetworkServer.SendToAll(new KeepAlive());
    }
}
