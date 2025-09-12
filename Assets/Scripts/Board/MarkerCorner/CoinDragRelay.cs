using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class CoinDragRelay : NetworkBehaviour
{
    public static CoinDragRelay Instance { get; private set; }

    // key -> CoinDragSync
    static readonly Dictionary<string, CoinDragSync> _targets = new Dictionary<string, CoinDragSync>();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static void Register(string key, CoinDragSync sync)
    {
        if (string.IsNullOrEmpty(key) || !sync) return;
        _targets[key] = sync;
    }

    public static void Unregister(string key, CoinDragSync sync)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_targets.TryGetValue(key, out var current) && current == sync) _targets.Remove(key);
    }

    // ---------------- client -> server ----------------

    [Command(requiresAuthority = false)]
    public void CmdBegin(string key, uint ownerNetId)
    {
        // (Optional) basic sanity: do not hard-fail if ownerNetId mismatches; you can add extra checks here.
        RpcBegin(key);
    }

    [Command(requiresAuthority = false)]
    public void CmdUpdate(string key, Vector2 anchored)
    {
        RpcUpdate(key, anchored);
    }

    // parentPath = transform path (relative to root canvas or another agreed root)
    [Command(requiresAuthority = false)]
    public void CmdEnd(string key, Vector2 anchored, string parentPath)
    {
        RpcEnd(key, anchored, parentPath);
    }

    // ---------------- server -> all clients ----------------

    [ClientRpc]
    void RpcBegin(string key)
    {
        if (_targets.TryGetValue(key, out var t)) t.RemoteBeginDrag();
    }

    [ClientRpc]
    void RpcUpdate(string key, Vector2 anchored)
    {
        if (_targets.TryGetValue(key, out var t)) t.RemoteUpdateDrag(anchored);
    }

    [ClientRpc]
    void RpcEnd(string key, Vector2 anchored, string parentPath)
    {
        if (_targets.TryGetValue(key, out var t)) t.RemoteEndDrag(anchored, parentPath);
    }
}
