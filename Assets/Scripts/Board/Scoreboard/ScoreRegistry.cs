using System;
using System.Collections.Generic;

public static class ScoreRegistry
{
    public static event Action<string, int> OnScoreChanged;

    static readonly Dictionary<string, int> _scores = new();

    public static IReadOnlyDictionary<string, int> GetAll() => _scores;

    public static int GetScore(string name)
    {
        return (name != null && _scores.TryGetValue(name, out var s)) ? s : 0;
    }

    public static void SetScore(string name, int score)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _scores[name] = score;
        OnScoreChanged?.Invoke(name, score);
    }

    public static void AddScore(string name, int delta)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var s = GetScore(name) + delta;
        _scores[name] = s;
        OnScoreChanged?.Invoke(name, s);
    }

    public static void InitializeScoresForRound()
    {
        foreach (var name in RosterStore.Instance.Names)
            SetScore(name, 0);
    }
}
