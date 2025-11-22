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

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (playGameButton != null)
                playGameButton.interactable = false;
        }

        public void ResetLobbyUI()
        {
            playerNameTexts.Clear();
            playerLobbyHandlers.Clear();

            if (playerListParent != null)
            {
                foreach (Transform child in playerListParent)
                {
                    var txt = child.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt) txt.text = string.Empty;

                    var handler = child.GetComponent<PlayerLobbyHandler>();
                    if (handler)
                    {
                        handler.isReady = false;
                    }
                }
            }

            if (playGameButton != null)
                playGameButton.interactable = false;
        }

        public void UpdatePlayerLobbyUI()
        {
            if (SteamLobby.Instance == null || SteamLobby.Instance.lobbyID == 0 || playerListParent == null)
            {
                ResetLobbyUI();
                return;
            }

            var lobby = new CSteamID(SteamLobby.Instance.lobbyID);
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);

            if (memberCount <= 0)
            {
                ResetLobbyUI();
                return;
            }

            CSteamID hostID = new CSteamID(ulong.Parse(SteamMatchmaking.GetLobbyData(lobby, "HostAddress")));
            List<CSteamID> orderedMembers = new List<CSteamID> { hostID };

            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                if (memberID != hostID)
                    orderedMembers.Add(memberID);
            }

            int uiCount = playerListParent.childCount;
            if (uiCount == 0)
            {
                StartCoroutine(RetryUpdate());
                return;
            }

            playerLobbyHandlers.Clear();
            for (int i = 0; i < uiCount; i++)
            {
                var handler = playerListParent.GetChild(i).GetComponent<PlayerLobbyHandler>();
                if (handler)
                    playerLobbyHandlers.Add(handler);
            }

            playerNameTexts.Clear();

            int assignCount = Mathf.Min(uiCount, orderedMembers.Count);

            for (int i = 0; i < assignCount; i++)
            {
                var member = orderedMembers[i];
                var child = playerListParent.GetChild(i);

                TextMeshProUGUI txtMesh = child.GetComponentInChildren<TextMeshProUGUI>();
                if (!txtMesh) continue;

                string playerName = SteamFriends.GetFriendPersonaName(member);
                txtMesh.text = playerName;
                playerNameTexts.Add(txtMesh);
            }

            for (int i = assignCount; i < uiCount; i++)
            {
                var child = playerListParent.GetChild(i);
                var txtMesh = child.GetComponentInChildren<TextMeshProUGUI>();
                if (txtMesh)
                    txtMesh.text = string.Empty;
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
            if (playerListParent == null || player == null)
                return;

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
            if (playGameButton != null)
                playGameButton.interactable = truthStatus;
        }

        private IEnumerator RetryUpdate()
        {
            yield return new WaitForSeconds(1f);
            UpdatePlayerLobbyUI();
        }
    }
}
