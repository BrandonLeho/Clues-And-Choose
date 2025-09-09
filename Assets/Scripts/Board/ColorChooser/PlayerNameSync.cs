using Mirror;
using UnityEngine;
using Steamworks;

public class PlayerNameSync : NetworkBehaviour
{
    [SyncVar] public string DisplayName;

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

    [Command]
    void CmdSetDisplayName(string name)
    {
        DisplayName = string.IsNullOrWhiteSpace(name) ? $"Player {netId}" : name.Trim();
    }
}
