using UnityEngine;
using TMPro;

public class RoundTurnSummaryUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    int _lastRoundIndex = int.MinValue;
    int _lastFullCycles = int.MinValue;
    string _lastClueName;

    void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();
    }

    void Update()
    {
        var rm = RoundManager.Instance;
        if (!rm || !label) return;

        int roundIndex = rm.CurrentRoundIndex;
        int fullCycles = rm.FullCyclesCompleted;
        int maxCycles = rm.MaxFullCycles;

        string clueName = RosterStore.CurrentClueGiverName;
        int playerCount = (RosterStore.Instance != null && RosterStore.Instance.Names != null)
            ? RosterStore.Instance.Names.Count
            : 0;

        if (roundIndex == _lastRoundIndex &&
            fullCycles == _lastFullCycles &&
            clueName == _lastClueName)
        {
            return;
        }

        _lastRoundIndex = roundIndex;
        _lastFullCycles = fullCycles;
        _lastClueName = clueName;

        int displayRound = Mathf.Max(0, roundIndex) + 1;

        int totalTurns = (playerCount > 0) ? playerCount * maxCycles : 0;
        string turnPart = totalTurns > 0
            ? $"Turn {displayRound}/{totalTurns}"
            : $"Turn {displayRound}";

        int currentCycle = Mathf.Clamp(fullCycles + 1, 1, maxCycles);
        string cyclePart = $"Cycle {currentCycle}/{maxCycles}";

        string cluePart = string.IsNullOrWhiteSpace(clueName)
            ? "Clue: —"
            : $"Clue: {clueName}";

        label.text = $"Round {displayRound} • {turnPart} • {cyclePart} • {cluePart}";
    }
}
