using Mirror;
using UnityEngine;
using Steamworks;

public class PlayerNameSync : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnDisplayNameChanged))] public string DisplayName;

    string _registeredNameServer;

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        string local =
#if !UNITY_SERVER && STEAMWORKS_NET
            SteamFriends.GetPersonaName();
#else
            System.Environment.UserName;
#endif
        if (string.IsNullOrWhiteSpace(local)) local = $"Player {netId}";
        CmdSetDisplayName(local);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!string.IsNullOrWhiteSpace(DisplayName))
            RosterStore.SaveOrUpdateName(netId, DisplayName);
    }

    void OnDisplayNameChanged(string _, string newName)
    {
        if (!string.IsNullOrWhiteSpace(newName))
            RosterStore.SaveOrUpdateName(netId, newName);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!string.IsNullOrWhiteSpace(DisplayName))
            ServerRegister(DisplayName);
    }

    public override void OnStopServer()
    {
        ServerUnregister(_registeredNameServer);
        base.OnStopServer();
    }

    [Command]
    void CmdSetDisplayName(string name)
    {
        DisplayName = string.IsNullOrWhiteSpace(name) ? $"Player {netId}" : name.Trim();

        ServerUnregister(_registeredNameServer);
        ServerRegister(DisplayName);
    }

    [Server]
    void ServerRegister(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (NameToNetIdRegistry.Instance)
        {
            NameToNetIdRegistry.Instance.ServerRegister(name, netId);
            _registeredNameServer = name;
        }
    }

    [Server]
    void ServerUnregister(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (NameToNetIdRegistry.Instance)
            NameToNetIdRegistry.Instance.ServerUnregister(name, netId);
        if (_registeredNameServer == name) _registeredNameServer = null;
    }
}
