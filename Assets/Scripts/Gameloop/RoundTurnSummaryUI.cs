using UnityEngine;
using TMPro;

public class RoundTurnSummaryUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    int _lastRoundCycleIndex = int.MinValue;
    int _lastRoundCycleMax = int.MinValue;
    int _lastPlacementCycle = int.MinValue;
    int _lastTurnIndex = int.MinValue;
    int _lastTurnMax = int.MinValue;
    string _lastClueName;

    int _currentTurnIndex;
    int _lastSeenPlacementCycle;
    uint _lastPlacerNetId;

    void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
    }

    void HandlePlacerChanged(uint placerNetId)
    {
        var rm = RoundManager.Instance;
        uint clueId = rm ? rm.CurrentClueGiverNetId : 0u;

        if (placerNetId == 0 || placerNetId == clueId)
        {
            _lastPlacerNetId = placerNetId;
            return;
        }

        if (placerNetId != _lastPlacerNetId)
        {
            _lastPlacerNetId = placerNetId;
            _currentTurnIndex++;
        }
    }

    void ResetTurnForNewCycle()
    {
        _currentTurnIndex = 0;
        _lastPlacerNetId = 0;
    }

    void Update()
    {
        if (!label) return;

        var rm = RoundManager.Instance;
        if (!rm) return;

        var pc = PhaseController.Instance;

        int fullCycles = rm.FullCyclesCompleted;
        int maxFullCycles = rm.MaxFullCycles;
        int roundCycleIndex = Mathf.Clamp(fullCycles + 1, 1, maxFullCycles);

        int placementCycle = pc ? pc.CurrentPlacementCycleDisplay : 0;
        int maxPlacementCycles = pc ? pc.MaxPlacementCyclesDisplay : 2;

        if (placementCycle != _lastSeenPlacementCycle)
        {
            _lastSeenPlacementCycle = placementCycle;
            if (placementCycle >= 1)
                ResetTurnForNewCycle();
        }

        int playerCount = (RosterStore.Instance != null && RosterStore.Instance.Names != null)
            ? RosterStore.Instance.Names.Count
            : 0;
        int nonCluePlayers = Mathf.Max(0, playerCount - 1);

        int displayTurnIndex = Mathf.Clamp(_currentTurnIndex, 0, nonCluePlayers);

        string clueName = RosterStore.CurrentClueGiverName;

        if (roundCycleIndex == _lastRoundCycleIndex &&
            maxFullCycles == _lastRoundCycleMax &&
            placementCycle == _lastPlacementCycle &&
            displayTurnIndex == _lastTurnIndex &&
            nonCluePlayers == _lastTurnMax &&
            clueName == _lastClueName)
        {
            return;
        }

        _lastRoundCycleIndex = roundCycleIndex;
        _lastRoundCycleMax = maxFullCycles;
        _lastPlacementCycle = placementCycle;
        _lastTurnIndex = displayTurnIndex;
        _lastTurnMax = nonCluePlayers;
        _lastClueName = clueName;

        string roundPart = $"Round {roundCycleIndex}/{maxFullCycles}";

        string cyclePart = placementCycle > 0
            ? $"Cycle {placementCycle}/{maxPlacementCycles}"
            : $"Cycle -/{maxPlacementCycles}";

        string turnPart;
        if (nonCluePlayers > 0 && displayTurnIndex > 0)
            turnPart = $"Turn {displayTurnIndex}/{nonCluePlayers}";
        else if (nonCluePlayers > 0)
            turnPart = $"Turn -/{nonCluePlayers}";
        else
            turnPart = "Turn -/-";

        string cluePart = string.IsNullOrWhiteSpace(clueName)
            ? "Clue Giver: —"
            : $"Clue Giver: {clueName}";

        label.text = $"{roundPart} • {cyclePart} • {turnPart} • {cluePart}";
    }
}
