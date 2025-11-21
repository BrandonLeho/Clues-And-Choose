using System.Collections.Generic;
using UnityEngine;

public class ScoreHistoryRecorder : MonoBehaviour
{
    public static ScoreHistoryRecorder Instance { get; private set; }

    [System.Serializable]
    public class PlayerSeries
    {
        public string name;
        public List<int> scores = new List<int>();
    }

    [Header("Debug")]
    [SerializeField] bool debugLogsEnabled = false;

    readonly Dictionary<string, PlayerSeries> _seriesByName = new Dictionary<string, PlayerSeries>();
    readonly List<int> _roundIndices = new List<int>();

    int _lastRecordedRoundIndex = int.MinValue;
    bool _subscribedToRoundManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        TrySubscribeToRoundManager();
    }

    void OnDisable()
    {
        UnsubscribeFromRoundManager();
    }

    void Update()
    {
        if (!_subscribedToRoundManager)
            TrySubscribeToRoundManager();
    }

    void TrySubscribeToRoundManager()
    {
        var rm = RoundManager.Instance;
        if (rm == null) return;

        rm.onRoundChangedClient.RemoveListener(HandleRoundChangedClient);
        rm.onRoundChangedClient.AddListener(HandleRoundChangedClient);
        _subscribedToRoundManager = true;
    }

    void UnsubscribeFromRoundManager()
    {
        var rm = RoundManager.Instance;
        if (rm != null)
            rm.onRoundChangedClient.RemoveListener(HandleRoundChangedClient);

        _subscribedToRoundManager = false;
    }

    void HandleRoundChangedClient(int roundIndex, uint clueGiverNetId)
    {
        if (roundIndex == _lastRecordedRoundIndex)
            return;

        if (roundIndex < 0)
            return;

        RecordSnapshot(roundIndex);
    }

    void RecordSnapshot(int roundIndex)
    {
        _lastRecordedRoundIndex = roundIndex;

        var roster = RosterStore.Instance;
        if (roster == null || roster.Names == null || roster.Names.Count == 0)
        {
            if (debugLogsEnabled)
                Debug.Log("[ScoreHistory] No roster; cannot snapshot.");
            return;
        }

        if (debugLogsEnabled)
            Debug.Log($"[ScoreHistory] Recording snapshot for round index {roundIndex}");

        _roundIndices.Add(roundIndex);

        foreach (var name in roster.Names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!_seriesByName.TryGetValue(name, out var series))
            {
                series = new PlayerSeries { name = name };
                _seriesByName.Add(name, series);
            }

            int score = ScoreRegistry.GetScore(name);
            series.scores.Add(score);
        }
    }

    public void RecordSnapshotNow()
    {
        int roundIndex = RoundManager.Instance != null
            ? RoundManager.Instance.CurrentRoundIndex
            : (_roundIndices.Count > 0 ? _roundIndices[_roundIndices.Count - 1] : 0);

        if (roundIndex == _lastRecordedRoundIndex)
        {
            if (debugLogsEnabled)
                Debug.Log("[ScoreHistory] RecordSnapshotNow skipped (already recorded this round).");
            return;
        }

        RecordSnapshot(roundIndex);
    }

    public IReadOnlyList<int> GetRoundIndices() => _roundIndices;

    public IEnumerable<PlayerSeries> GetAllSeries()
    {
        return _seriesByName.Values;
    }

    public PlayerSeries GetSeriesFor(string playerName)
    {
        _seriesByName.TryGetValue(playerName, out var s);
        return s;
    }
}