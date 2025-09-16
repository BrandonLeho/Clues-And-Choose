using Mirror;
using UnityEngine;

public struct KeepAlive : NetworkMessage { }
public class NetKeepAlive : MonoBehaviour
{
    [SerializeField] float interval = 1.0f;

    float timer;
    static bool registered;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (!registered)
        {
            NetworkClient.RegisterHandler<KeepAlive>(_ => { /* no-op */ });
            NetworkServer.RegisterHandler<KeepAlive>((conn, msg) => { /* no-op */ });
            registered = true;
        }
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < interval) return;
        timer = 0f;

        if (NetworkClient.isConnected)
            NetworkClient.Send(new KeepAlive());

        if (NetworkServer.active && NetworkServer.connections.Count > 0)
            NetworkServer.SendToAll(new KeepAlive());
    }
}
