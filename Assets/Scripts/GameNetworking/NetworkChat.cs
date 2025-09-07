using System;
using Mirror;
using UnityEngine;

// Optional Steamworks (guarded by preprocessor in case you build without Steam)
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Lives on the player object. Owns the local "Send" command and relays
/// chat messages from server to all clients.
/// </summary>
public class NetworkChat : NetworkBehaviour
{
    // Fired on every client whenever a new chat message arrives.
    public static event Action<string, string, double> OnMessage;

    [SyncVar] public string DisplayName;

    // Called on the local client when *this* player gains authority.
    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        // Try to set a friendly display name once (server will store it)
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
        // Minimal sanitize/limit
        if (string.IsNullOrWhiteSpace(name)) name = $"Player {netId}";
        DisplayName = name.Trim().Substring(0, Mathf.Min(24, name.Trim().Length));
    }

    /// <summary>Local UI calls this to send a message.</summary>
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
        // Basic server-side guardrails
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 300) text = text.Substring(0, 300);

        string senderName = DisplayName;
        double sentAt = NetworkTime.time; // server time

        // Broadcast to everyone
        RpcReceive(senderName, text, sentAt);
    }

    [ClientRpc]
    void RpcReceive(string from, string text, double sentAt)
    {
        OnMessage?.Invoke(from, text, sentAt);
    }
}
