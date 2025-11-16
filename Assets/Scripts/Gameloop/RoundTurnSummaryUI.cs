using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoundTurnSummaryUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    int _currentPlacementCycleIndex = 1;
    int _currentTurnNumber = 0;
    int _lastNonCluePlayerCount = 0;

    readonly HashSet<uint> _placersThisCycle = new HashSet<uint>();
    uint _currentPlacerNetId = 0;

    int _lastRoundCycleIndex = -1;
    int _lastMaxFullCycles = -1;
    int _lastShownPlacementCycleIndex = -1;
    int _lastShownTurnNumber = -1;
    string _lastClueName;
    string _lastLabelText;

    const int PlacementCycleMax = 2;

    void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        ResetCycleState();

        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        RoundManager.Instance?.onRoundChangedClient?.RemoveListener(HandleClientRoundChanged);
        RoundManager.Instance?.onRoundChangedClient?.AddListener(HandleClientRoundChanged);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        RoundManager.Instance?.onRoundChangedClient?.RemoveListener(HandleClientRoundChanged);
    }

    void ResetCycleState()
    {
        _placersThisCycle.Clear();
        _currentPlacerNetId = 0;
        _currentPlacementCycleIndex = 1;
        _currentTurnNumber = 0;
        _lastNonCluePlayerCount = 0;
    }

    void HandleClientRoundChanged(int _, uint __)
    {
        ResetCycleState();
    }

    void HandlePlacerChanged(uint placerNetId)
    {
        _currentPlacerNetId = placerNetId;

        var rm = RoundManager.Instance;
        if (!rm)
        {
            _currentTurnNumber = 0;
            return;
        }

        uint clueNetId = rm.CurrentClueGiverNetId;

        if (placerNetId == 0)
        {
            _currentTurnNumber = 0;
            return;
        }

        if (clueNetId != 0 && placerNetId == clueNetId)
        {
            _currentTurnNumber = 0;
            return;
        }

        int playerCount = (RosterStore.Instance != null && RosterStore.Instance.Names != null)
            ? RosterStore.Instance.Names.Count
            : 0;

        int nonClueCount = playerCount - (clueNetId != 0 ? 1 : 0);
        if (nonClueCount < 1)
            nonClueCount = 0;

        _lastNonCluePlayerCount = nonClueCount;

        bool alreadySeenThisCycle = _placersThisCycle.Contains(placerNetId);

        if (!alreadySeenThisCycle)
        {
            _placersThisCycle.Add(placerNetId);

            if (nonClueCount > 0 && _placersThisCycle.Count > nonClueCount)
            {
                _currentPlacementCycleIndex = 2;
                _placersThisCycle.Clear();
                _placersThisCycle.Add(placerNetId);
            }
        }

        _currentTurnNumber = _placersThisCycle.Count;
    }

    void Update()
    {
        var rm = RoundManager.Instance;
        if (!rm || !label) return;

        int maxFullCycles = rm.MaxFullCycles;
        int fullCyclesCompleted = rm.FullCyclesCompleted;
        int roundCycleIndex = Mathf.Clamp(fullCyclesCompleted + 1, 1, maxFullCycles);

        string clueName = RosterStore.CurrentClueGiverName;

        int playerCount = (RosterStore.Instance != null && RosterStore.Instance.Names != null)
            ? RosterStore.Instance.Names.Count
            : 0;

        uint clueNetId = rm.CurrentClueGiverNetId;
        int nonClueCount = playerCount - (clueNetId != 0 ? 1 : 0);
        if (nonClueCount < 0) nonClueCount = 0;

        _lastNonCluePlayerCount = nonClueCount;

        string roundPart = $"Round {roundCycleIndex}/{maxFullCycles}";

        int placementCycleIdx = Mathf.Clamp(_currentPlacementCycleIndex, 1, PlacementCycleMax);
        string cyclePart = $"Cycle {placementCycleIdx}/{PlacementCycleMax}";

        string turnPart;
        if (nonClueCount > 0 && _currentTurnNumber > 0)
        {
            int clampedTurn = Mathf.Clamp(_currentTurnNumber, 1, nonClueCount);
            turnPart = $"Turn {clampedTurn}/{nonClueCount}";
        }
        else if (nonClueCount > 0)
        {
            turnPart = $"Turn 0/{nonClueCount}";
        }
        else
        {
            turnPart = "Turn —";
        }

        string cluePart = string.IsNullOrWhiteSpace(clueName)
            ? "Clue Giver: —"
            : $"Clue Giver: {clueName}";

        string text = $"{roundPart} • {cyclePart} • {turnPart} • {cluePart}";

        if (roundCycleIndex == _lastRoundCycleIndex &&
            maxFullCycles == _lastMaxFullCycles &&
            placementCycleIdx == _lastShownPlacementCycleIndex &&
            _currentTurnNumber == _lastShownTurnNumber &&
            clueName == _lastClueName &&
            text == _lastLabelText)
        {
            return;
        }

        _lastRoundCycleIndex = roundCycleIndex;
        _lastMaxFullCycles = maxFullCycles;
        _lastShownPlacementCycleIndex = placementCycleIdx;
        _lastShownTurnNumber = _currentTurnNumber;
        _lastClueName = clueName;
        _lastLabelText = text;

        label.text = text;
    }
}
