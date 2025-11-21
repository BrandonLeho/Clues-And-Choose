using Mirror;
using UnityEngine;

public class ScoreboardReveal : NetworkBehaviour
{
    [SerializeField] GameObject scoreboardUI;
    [SerializeField] ColorGridOutroAnimator gridOutroAnimator;

    bool _waitingForOutro;

    void OnEnable()
    {
        RoundManager.OnServerClueGiverCyclesFinished += HandleGameEnded;
    }

    void OnDisable()
    {
        RoundManager.OnServerClueGiverCyclesFinished -= HandleGameEnded;

        if (gridOutroAnimator != null)
        {
            gridOutroAnimator.OnAnimationComplete.RemoveListener(HandleGridOutroComplete);
        }
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

    public void OnGameFinishedBannerComplete()
    {
        if (gridOutroAnimator != null)
        {
            if (!_waitingForOutro)
            {
                _waitingForOutro = true;
                gridOutroAnimator.OnAnimationComplete.RemoveListener(HandleGridOutroComplete);
                gridOutroAnimator.OnAnimationComplete.AddListener(HandleGridOutroComplete);
                gridOutroAnimator.Play();
            }
        }
        else
        {
            ShowScoreboardNow();
        }
    }

    void HandleGridOutroComplete()
    {
        _waitingForOutro = false;
        ShowScoreboardNow();
    }

    public void ShowScoreboardNow()
    {
        if (ScoreHistoryRecorder.Instance != null)
        {
            ScoreHistoryRecorder.Instance.RecordSnapshotNow();
        }

        if (scoreboardUI)
            scoreboardUI.SetActive(true);

        var ui = scoreboardUI.GetComponent<EndGameScoreboardUI>();
        if (ui)
            ui.Refresh();
    }
}
