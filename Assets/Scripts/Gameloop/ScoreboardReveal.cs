using Mirror;
using UnityEngine;

public class ScoreboardReveal : NetworkBehaviour
{
    [SerializeField] GameObject scoreboardUI;

    void OnEnable()
    {
        RoundManager.OnServerClueGiverCyclesFinished += HandleGameEnded;
    }

    void OnDisable()
    {
        RoundManager.OnServerClueGiverCyclesFinished -= HandleGameEnded;
    }

    void HandleGameEnded()
    {
        RpcShowScoreboard();
    }

    [ClientRpc]
    void RpcShowScoreboard()
    {
        var tn = FindFirstObjectByType<TurnNotification>();
        if (tn != null)
        {
            tn.PlaySystemMessage("GAME FINISHED", false, true);
        }
        else
        {
            ShowScoreboardNow();
        }
    }

    public void ShowScoreboardNow()
    {
        if (scoreboardUI)
            scoreboardUI.SetActive(true);

        var ui = scoreboardUI.GetComponent<EndGameScoreboardUI>();
        if (ui)
            ui.Refresh();
    }
}
