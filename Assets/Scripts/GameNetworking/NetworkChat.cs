using System;
using Mirror;
using UnityEngine;


#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class NetworkChat : NetworkBehaviour
{
    public static event Action<string, string, double> OnMessage;

    [SyncVar] public string DisplayName;

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        if (isOwned)
        {
            string name = $"Player {netId}";
#if !DISABLESTEAMWORKS
            try
            {
                if (SteamManager.Initialized)
                    name = SteamFriends.GetPersonaName();
            }
            catch { /* safe fallback */ }
#endif
            CmdSetDisplayName(name);
        }
    }

    [Command]
    void CmdSetDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = $"Player {netId}";
        DisplayName = name.Trim().Substring(0, Mathf.Min(24, name.Trim().Length));
    }

    public void Send(string text)
    {
        if (!isLocalPlayer) return;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        CmdSend(text);
    }

    [Command]
    void CmdSend(string text, NetworkConnectionToClient sender = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 300) text = text.Substring(0, 300);

        string senderName = DisplayName;
        double sentAt = NetworkTime.time;

        RpcReceive(senderName, text, sentAt);
    }

    [ClientRpc]
    void RpcReceive(string from, string text, double sentAt)
    {
        OnMessage?.Invoke(from, text, sentAt);
    }
}
