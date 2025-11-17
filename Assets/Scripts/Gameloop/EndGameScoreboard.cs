using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class EndGameScoreboardConsole : NetworkBehaviour
{
    [Header("Options")]
    [SerializeField] bool autoPrintOnCyclesFinished = true;
    [SerializeField] bool debugLogsEnabled = true;

    void OnEnable()
    {
        if (autoPrintOnCyclesFinished)
            RoundManager.OnServerClueGiverCyclesFinished += HandleCyclesFinished;
    }

    void OnDisable()
    {
        if (autoPrintOnCyclesFinished)
            RoundManager.OnServerClueGiverCyclesFinished -= HandleCyclesFinished;
    }

    [Server]
    void HandleCyclesFinished()
    {
        PrintScoreboardToConsole();
    }

    [ContextMenu("Debug Print Scoreboard")]
    public void DebugPrintScoreboardContextMenu()
    {
        PrintScoreboardToConsole();
    }

    void DLog(object msg)
    {
        if (debugLogsEnabled) Debug.Log(msg);
    }

    public void PrintScoreboardToConsole()
    {
        var allScores = ScoreRegistry.GetAll();
        if (allScores == null || allScores.Count == 0)
        {
            DLog("[Scoreboard] No scores recorded.");
            return;
        }

        var entries = new List<ScoreEntry>();
        foreach (var kv in allScores)
        {
            string name = kv.Key;
            int score = kv.Value;
            Color color;
            if (!RegistryNameColorLookup.TryGetColorForName(name, out color))
                color = Color.white;

            entries.Add(new ScoreEntry
            {
                Name = name,
                Score = score,
                Color = color
            });
        }

        entries = entries
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Name)
            .ToList();

        int roundIndex = RoundManager.Instance ? RoundManager.Instance.CurrentRoundIndex : -1;
        DLog("==================================");
        DLog("[Scoreboard] FINAL SCORES");

        if (roundIndex >= 0)
            DLog("[Scoreboard] Final Round Index: " + roundIndex);

        DLog("[Scoreboard] Players: " + entries.Count);
        DLog("----------------------------------");

        int rank = 1;
        foreach (var e in entries)
        {
            string hex = ColorUtility.ToHtmlStringRGB(e.Color);
            int r = Mathf.RoundToInt(e.Color.r * 255f);
            int g = Mathf.RoundToInt(e.Color.g * 255f);
            int b = Mathf.RoundToInt(e.Color.b * 255f);

            DLog(
                $"{rank,2}. {e.Name,-16} " +
                $"Score: {e.Score,3}  " +
                $"Color: #{hex} (RGB {r},{g},{b})"
            );
            rank++;
        }

        if (entries.Count > 0)
        {
            int bestScore = entries[0].Score;
            var winners = entries.Where(e => e.Score == bestScore).ToList();

            DLog("----------------------------------");
            if (winners.Count == 1)
            {
                var w = winners[0];
                string hex = ColorUtility.ToHtmlStringRGB(w.Color);
                DLog($"[Scoreboard] WINNER: {w.Name} with {bestScore} pts (#{hex})");
            }
            else
            {
                var names = string.Join(", ", winners.Select(w => w.Name));
                DLog($"[Scoreboard] TIE WINNERS ({bestScore} pts): {names}");
            }
        }

        DLog("==================================");
    }

    struct ScoreEntry
    {
        public string Name;
        public int Score;
        public Color Color;
    }
}
