using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

// A single networked object that tracks players across scenes.
public class LobbyRoster : NetworkBehaviour
{
    public static LobbyRoster Instance;

    // SteamID (or connectionId fallback) -> DisplayName
    public class SyncDictULongString : SyncDictionary<ulong, string> { }
    [SyncVar] public string lobbyName = "Default Lobby"; // optional
    public SyncDictULongString players = new SyncDictULongString();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Keep this object across scenes on server and clients.
        DontDestroyOnLoad(gameObject);
    }

    [Server]
    public void ServerAddOrUpdate(ulong key, string displayName)
    {
        players[key] = displayName;
    }

    [Server]
    public void ServerRemove(ulong key)
    {
        if (players.ContainsKey(key)) players.Remove(key);
    }
}
