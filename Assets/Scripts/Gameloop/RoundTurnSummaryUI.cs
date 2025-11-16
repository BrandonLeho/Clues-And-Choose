using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoundTurnSummaryUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    int _placementCycleDisplay = 1;
    int _currentTurnNumber = 0;

    readonly HashSet<uint> _placersThisCycle = new HashSet<uint>();

    int _lastRoundCycleIndex = -1;
    int _lastMaxFullCycles = -1;
    int _lastPlacementCycleDisplay = -1;
    int _lastTurnNumber = -1;
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
        ResetForNewRound();

        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.RemoveListener(HandleClientRoundChanged);
            RoundManager.Instance.onRoundChangedClient.AddListener(HandleClientRoundChanged);
        }

        PhaseController.OnClientRoundDecision += HandleClientRoundDecision;
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;

        if (RoundManager.Instance)
            RoundManager.Instance.onRoundChangedClient.RemoveListener(HandleClientRoundChanged);

        PhaseController.OnClientRoundDecision -= HandleClientRoundDecision;
    }

    void ResetForNewRound()
    {
        _placementCycleDisplay = 1;
        _currentTurnNumber = 0;
        _placersThisCycle.Clear();
    }

    void ResetTurnForNewCycle()
    {
        _currentTurnNumber = 0;
        _placersThisCycle.Clear();
    }

    void HandleClientRoundChanged(int _, uint __)
    {
        ResetForNewRound();
    }

    void HandleClientRoundDecision(bool endNow)
    {
        if (!endNow)
        {
            _placementCycleDisplay = 2;
            ResetTurnForNewCycle();
        }
        else
        {
            _placementCycleDisplay = 1;
        }
    }

    void HandlePlacerChanged(uint placerNetId)
    {
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

        if (!_placersThisCycle.Contains(placerNetId))
        {
            _placersThisCycle.Add(placerNetId);
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

        string roundPart = $"Round {roundCycleIndex}/{maxFullCycles}";

        int cycleIdx = Mathf.Clamp(_placementCycleDisplay, 1, PlacementCycleMax);
        string cyclePart = $"Cycle {cycleIdx}/{PlacementCycleMax}";

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
            cycleIdx == _lastPlacementCycleDisplay &&
            _currentTurnNumber == _lastTurnNumber &&
            clueName == _lastClueName &&
            text == _lastLabelText)
        {
            return;
        }

        _lastRoundCycleIndex = roundCycleIndex;
        _lastMaxFullCycles = maxFullCycles;
        _lastPlacementCycleDisplay = cycleIdx;
        _lastTurnNumber = _currentTurnNumber;
        _lastClueName = clueName;
        _lastLabelText = text;

        label.text = text;
    }
}
