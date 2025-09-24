using Mirror;
using UnityEngine;

public class RouletteToRoundBridge : NetworkBehaviour
{
    public RoundManager roundManager;

    void Awake()
    {
        if (!roundManager) roundManager = RoundManager.Instance;
    }

    public void OnRouletteSpinComplete(string winnerName, int winnerIndex)
    {
        if (isServer) ServerApplyWinnerByName(winnerName);
        else CmdApplyWinnerByName(winnerName);
    }

    [Command(requiresAuthority = false)]
    void CmdApplyWinnerByName(string winnerName)
    {
        ServerApplyWinnerByName(winnerName);
    }

    [Server]
    void ServerApplyWinnerByName(string winnerName)
    {
        if (!roundManager) roundManager = RoundManager.Instance;
        if (!roundManager || !NameToNetIdRegistry.Instance) return;

        if (NameToNetIdRegistry.Instance.TryGetNetId(winnerName, out var netId))
        {
            roundManager.ServerSetInitialClueGiver(netId);
        }
        else
        {
            Debug.LogWarning($"[RouletteBridge] No netId mapped for winner '{winnerName}'. " +
                             "Verify PlayerNameSync is registering names on the server.");
        }
    }
}
