using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class EndGameScoreboardUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] Transform listParent;
    [SerializeField] GameObject rowPrefab;

    [Header("Bar Sizing")]
    [SerializeField] float minBarWidth = 20f;
    [SerializeField] float maxBarWidth = 400f;

    [Header("Winner Display")]
    [SerializeField] TextMeshProUGUI winnerLabel;

    [Header("Options")]
    [SerializeField] bool autoRefreshOnEnable = true;
    [SerializeField] bool autoRefreshOnScoreChanged = false;
    [SerializeField] bool debugLogsEnabled = false;

    void OnEnable()
    {
        if (autoRefreshOnScoreChanged)
            ScoreRegistry.OnScoreChanged += HandleScoreChanged;

        if (autoRefreshOnEnable)
            Refresh();
    }

    void OnDisable()
    {
        if (autoRefreshOnScoreChanged)
            ScoreRegistry.OnScoreChanged -= HandleScoreChanged;
    }

    void HandleScoreChanged(string _, int __)
    {
        Refresh();
    }

    void DLog(object msg)
    {
        if (debugLogsEnabled) Debug.Log(msg);
    }

    [ContextMenu("Refresh Scoreboard")]
    public void Refresh()
    {
        if (!listParent || !rowPrefab)
        {
            DLog("[ScoreboardUI] Missing listParent or rowPrefab.");
            return;
        }

        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        var names = new List<string>();

        if (RosterStore.Instance != null && RosterStore.Instance.Names != null && RosterStore.Instance.Names.Count > 0)
        {
            names.AddRange(RosterStore.Instance.Names);
        }
        else
        {
            var scoresDict = ScoreRegistry.GetAll();
            if (scoresDict != null)
                names.AddRange(scoresDict.Keys);
        }

        if (names.Count == 0)
        {
            DLog("[ScoreboardUI] No player names found.");
            if (winnerLabel) winnerLabel.text = "No players.";
            return;
        }

        var entries = new List<ScoreEntry>();
        foreach (var name in names)
        {
            int score = ScoreRegistry.GetScore(name);
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

        int maxScore = 0;
        foreach (var e in entries)
            if (e.Score > maxScore) maxScore = e.Score;
        if (maxScore <= 0) maxScore = 1;

        foreach (var e in entries)
        {
            float t = Mathf.Clamp01((float)e.Score / maxScore);
            float width = Mathf.Lerp(minBarWidth, maxBarWidth, t);

            var rowObj = Instantiate(rowPrefab, listParent);
            var row = rowObj.GetComponent<EndGameScoreboardRow>();
            if (!row)
            {
                DLog("[ScoreboardUI] Row prefab missing EndGameScoreboardRow component.");
                continue;
            }

            row.Bind(e.Name, e.Score, e.Color, width);
        }

        if (winnerLabel)
        {
            int bestScore = entries[0].Score;
            var winners = entries.Where(e => e.Score == bestScore).ToList();

            if (winners.Count == 1)
            {
                var w = winners[0];
                winnerLabel.text = $"Winner: {w.Name} ({w.Score})";
                winnerLabel.color = w.Color;
            }
            else
            {
                string namesJoined = string.Join(", ", winners.Select(w => w.Name));
                winnerLabel.text = $"Winners: {namesJoined} ({bestScore})";
                winnerLabel.color = Color.white;
            }
        }
    }

    struct ScoreEntry
    {
        public string Name;
        public int Score;
        public Color Color;
    }
}
