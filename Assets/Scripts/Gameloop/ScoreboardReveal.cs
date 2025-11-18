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
        if (scoreboardUI)
            scoreboardUI.SetActive(true);

        var ui = scoreboardUI.GetComponent<EndGameScoreboardUI>();
        if (ui)
            ui.Refresh();
    }
}
