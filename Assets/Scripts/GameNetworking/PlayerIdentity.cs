using Mirror;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class PlayerIdentity : NetworkBehaviour
{
    [SyncVar] public ulong steamId;
    [SyncVar] public string displayName;

    public override void OnStartLocalPlayer()
    {
        // Local client sends their name/steamId up to server once they exist.
#if !DISABLESTEAMWORKS
        ulong sid = SteamUser.GetSteamID().m_SteamID;
        string name = SteamFriends.GetPersonaName();
#else
        ulong sid = (ulong)Random.Range(1, int.MaxValue);
        string name = $"Player {Random.Range(1000,9999)}";
#endif
        CmdSetIdentity(sid, name);
    }

    [Command]
    void CmdSetIdentity(ulong sid, string name)
    {
        steamId = sid;
        displayName = string.IsNullOrWhiteSpace(name) ? $"Player {connectionToClient.connectionId}" : name;

        // If the roster already exists, record this player
        if (LobbyRoster.Instance != null)
            LobbyRoster.Instance.ServerAddOrUpdate(steamId, displayName);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (LobbyRoster.Instance != null && steamId != 0)
            LobbyRoster.Instance.ServerRemove(steamId);
    }
}
