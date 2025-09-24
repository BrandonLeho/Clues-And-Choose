using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class NameToNetIdRegistry : NetworkBehaviour
{
    public static NameToNetIdRegistry Instance;

    readonly Dictionary<string, uint> _nameToNetId = new Dictionary<string, uint>();

    void Awake() => Instance = this;

    [Server]
    public void ServerRegister(string playerName, uint netId)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return;
        _nameToNetId[playerName] = netId;
    }

    [Server]
    public void ServerUnregister(string playerName, uint netId)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return;
        if (_nameToNetId.TryGetValue(playerName, out var existing) && existing == netId)
            _nameToNetId.Remove(playerName);
    }

    [Server]
    public bool TryGetNetId(string playerName, out uint netId)
        => _nameToNetId.TryGetValue(playerName, out netId);
}
