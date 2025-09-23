using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;
using TMPro;
using Steamworks;

namespace SteamLobbySpace
{
    public class PlayerLobbyHandlerGame : NetworkBehaviour
    {
        public TextMeshProUGUI nameText;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (string.IsNullOrEmpty(RosterStore.LocalPlayerName))
            {
                RosterStore.SetLocalPlayerName(SteamFriends.GetPersonaName());
            }
        }
    }
}

