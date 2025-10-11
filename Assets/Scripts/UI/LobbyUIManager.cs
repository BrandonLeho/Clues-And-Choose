using System.Collections.Generic;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using Steamworks;
using System.Linq;
using System;

namespace SteamLobbySpace
{
    public class LobbyUIManager : NetworkBehaviour
    {
        public static LobbyUIManager Instance;
        public Transform playerListParent;
        public List<TextMeshProUGUI> playerNameTexts = new List<TextMeshProUGUI>();
        public List<PlayerLobbyHandler> playerLobbyHandlers = new List<PlayerLobbyHandler>();
        public Button playGameButton;

        public IReadOnlyList<PlayerLobbyHandler> CurrentPlayers => playerLobbyHandlers;
        public IReadOnlyList<string> CurrentPlayerNames => playerNameTexts.ConvertAll(t => t.text);


        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            playGameButton.interactable = false;
        }

        public void UpdatePlayerLobbyUI()
        {
            playerNameTexts.Clear();
            playerLobbyHandlers.Clear();

            var lobby = new CSteamID(SteamLobby.Instance.lobbyID);
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);

            CSteamID hostID = new CSteamID(ulong.Parse(SteamMatchmaking.GetLobbyData(lobby, "HostAddress")));
            List<CSteamID> orderedMembers = new List<CSteamID>();

            if (memberCount == 0)
            {
                Debug.LogWarning("Lobby has no members.. retrying...");
                StartCoroutine(RetryUpdate());
                return;
            }

            orderedMembers.Add(hostID);

            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                if (memberID != hostID)
                {
                    orderedMembers.Add(memberID);
                }
            }

            int j = 0;
            foreach (var member in orderedMembers)
            {
                TextMeshProUGUI txtMesh = playerListParent.GetChild(j).GetChild(0).GetComponent<TextMeshProUGUI>();
                PlayerLobbyHandler playerLobbyHandler = playerListParent.GetChild(j).GetComponent<PlayerLobbyHandler>();

                playerLobbyHandlers.Add(playerLobbyHandler);
                playerNameTexts.Add(txtMesh);

                string playerName = SteamFriends.GetFriendPersonaName(member);
                playerNameTexts[j].text = playerName;
                j++;
            }
        }

        public void OnPlayButtonClicked()
        {
            if (!NetworkServer.active) return;

            var netIds = new List<uint>();
            var names = new List<string>();

            foreach (var kvp in NetworkServer.connections)
            {
                var conn = kvp.Value;
                if (conn == null || conn.identity == null) continue;

                var id = conn.identity;
                var pns = id.GetComponent<PlayerNameSync>();

                string displayName = (pns != null && !string.IsNullOrWhiteSpace(pns.DisplayName))
                    ? pns.DisplayName.Trim()
                    : null;

                if (string.IsNullOrEmpty(displayName))
                {
                    int idx = netIds.Count;
                    if (idx < CurrentPlayerNames.Count)
                        displayName = CurrentPlayerNames[idx];
                    else
                        displayName = id.gameObject.name;
                }

                netIds.Add(id.netId);
                names.Add(displayName);
            }

            RpcReceiveRoster(netIds.ToArray(), names.ToArray());
            CustomNetworkManager.singleton.ServerChangeScene("GameScene");
        }

        [ClientRpc]
        void RpcReceiveRoster(uint[] netIds, string[] names)
        {
            RosterStore.SaveRoster(netIds, names);
        }

        [TargetRpc]
        void TargetReceiveRoster(NetworkConnectionToClient conn, uint[] netIds, string[] names)
        {
            RosterStore.SaveRoster(netIds, names);
        }

        public void RegisterPlayer(PlayerLobbyHandler player)
        {
            player.transform.SetParent(playerListParent, false);
            UpdatePlayerLobbyUI();
        }

        [Server]
        public void CheckAllPlayersReady()
        {
            foreach (var player in playerLobbyHandlers)
            {
                if (!player.isReady)
                {
                    RpcSetPlayButtonInteractable(false);
                    return;
                }
            }
            RpcSetPlayButtonInteractable(true);
        }

        [ClientRpc]
        void RpcSetPlayButtonInteractable(bool truthStatus)
        {
            playGameButton.interactable = truthStatus;
        }

        private IEnumerator RetryUpdate()
        {
            yield return new WaitForSeconds(1f);
            UpdatePlayerLobbyUI();
        }

    }
}