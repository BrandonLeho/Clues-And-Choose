using UnityEngine;
using Mirror;
using UnityEngine.UI;
using TMPro;

public class CoinPlacementTimerUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Text timeLabelUI;
    [SerializeField] TextMeshProUGUI timeLabelTMP;
    [SerializeField] string prefix = "Time ";
    [SerializeField] bool hideWhenNotActive = false;

    [Header("Timing")]
    [SerializeField, Min(0.5f)] float turnDurationSeconds = 15f;

    [Header("Debug")]
    [SerializeField] bool debugLogs = false;

    bool _hasTargetGate;
    uint _currentPlacerNetId;

    float _remaining;
    bool _running;

    void Awake()
    {
        if (!timeLabelUI) timeLabelUI = GetComponent<Text>();
        if (!timeLabelTMP) timeLabelTMP = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        PhaseController.OnClientTargetChosen += HandleTargetChosen;
        PhaseController.OnClientRoundDecision += HandleRoundDecision;

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.AddListener(HandleRoundChanged);
            RoundManager.Instance.onClueGiverChangedClient.AddListener(HandleClueGiverChanged);
        }

        if (CoinPlacementTurnManager.Instance)
            _currentPlacerNetId = CoinPlacementTurnManager.Instance.currentPlacerNetId;

        _hasTargetGate = PhaseController.Instance && PhaseController.Instance.ClientHasTarget;

        UpdateTimerState(immediate: true);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientTargetChosen -= HandleTargetChosen;
        PhaseController.OnClientRoundDecision -= HandleRoundDecision;

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.RemoveListener(HandleRoundChanged);
            RoundManager.Instance.onClueGiverChangedClient.RemoveListener(HandleClueGiverChanged);
        }
    }

    void Update()
    {
        if (!_running) return;

        _remaining -= Time.unscaledDeltaTime;

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            Log("[Timer] Expired → attempting auto-drop and turn advance.");
            OnTimerExpired();
            StopTimer();
        }
        else
        {
            UpdateLabel(active: true, remainingSeconds: _remaining);
        }
    }

    void HandlePlacerChanged(uint netId)
    {
        _currentPlacerNetId = netId;
        if (debugLogs) Log($"[Timer] Placer changed → {_currentPlacerNetId}");

        UpdateTimerState(immediate: false);
    }

    void HandleTargetChosen(int col, int row, Color color)
    {
        _hasTargetGate = true;
        if (debugLogs) Log("[Timer] Target chosen → gate enabled");
        UpdateTimerState(immediate: false);
    }

    void HandleRoundChanged(int _, uint __)
    {
        _hasTargetGate = false;
        if (debugLogs) Log("[Timer] Round changed → gate reset / stop");
        UpdateTimerState(immediate: false);
    }

    void HandleClueGiverChanged(uint ___)
    {
        _hasTargetGate = false;
        if (debugLogs) Log("[Timer] Clue giver changed → gate reset / stop");
        UpdateTimerState(immediate: false);
    }

    void HandleRoundDecision(bool endNow)
    {
        if (debugLogs)
            Log($"[Timer] RoundDecision → endNow={endNow} (stop timer).");

        StopTimer();
    }

    void UpdateTimerState(bool immediate)
    {
        if (!_hasTargetGate || _currentPlacerNetId == 0)
        {
            if (debugLogs)
                Log($"[Timer] UpdateTimerState → inactive (hasTargetGate={_hasTargetGate}, placer={_currentPlacerNetId})");

            StopTimer();
            return;
        }

        if (!_running || immediate)
        {
            _remaining = turnDurationSeconds;
            _running = true;
        }

        if (debugLogs)
        {
            bool isLocalTurn = CoinPlacementTurnManager.IsLocalPlayersTurn();
            Log($"[Timer] START/CONTINUE → placerNetId={_currentPlacerNetId}, isLocalTurn={isLocalTurn}, remaining={_remaining:0.00}s");
        }

        UpdateLabel(active: true, remainingSeconds: _remaining);
    }

    void StopTimer()
    {
        if (!_running)
        {
            UpdateLabel(active: false, remainingSeconds: 0f);
            return;
        }

        if (debugLogs)
            Log("[Timer] StopTimer.");

        _running = false;
        UpdateLabel(active: false, remainingSeconds: 0f);
    }

    void OnTimerExpired()
    {
        bool isLocalTurn = CoinPlacementTurnManager.IsLocalPlayersTurn();
        if (isLocalTurn)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → local player's turn, forcing coin drop if dragging.");

            CoinDragHandler.ForceDropIfDragging();
        }
        else if (debugLogs)
        {
            Log("[Timer] OnTimerExpired → not local player's turn, skip local force-drop.");
        }

        if (!NetworkServer.active)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → not server, skipping ServerNoteSuccessfulPlacement.");
            return;
        }

        var tm = CoinPlacementTurnManager.Instance;
        if (!tm)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → no CoinPlacementTurnManager on server.");
            return;
        }

        uint placer = _currentPlacerNetId;
        if (placer == 0)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → no active placer on server.");
            return;
        }

        if (debugLogs)
            Log($"[Timer] OnTimerExpired → server forcing advance for placer {placer} (counts even if coin is invalid/home).");

        tm.ServerNoteSuccessfulPlacement(placer);
    }

    void UpdateLabel(bool active, float remainingSeconds)
    {
        string textToShow;

        if (!active)
        {
            if (hideWhenNotActive)
            {
                SetLabelActive(false);
                return;
            }

            textToShow = prefix;
            SetLabelActive(true);
        }
        else
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            textToShow = prefix + seconds;
            SetLabelActive(true);
        }

        if (timeLabelUI) timeLabelUI.text = textToShow;
        if (timeLabelTMP) timeLabelTMP.text = textToShow;
    }

    void SetLabelActive(bool v)
    {
        if (timeLabelUI && timeLabelUI.gameObject.activeSelf != v)
            timeLabelUI.gameObject.SetActive(v);
        if (timeLabelTMP && timeLabelTMP.gameObject.activeSelf != v)
            timeLabelTMP.gameObject.SetActive(v);
    }

    void Log(string msg)
    {
        if (!debugLogs) return;

        string who = NetworkClient.active
            ? (NetworkClient.connection?.identity
               ? $"[{NetworkClient.connection.identity.netId}]"
               : "[no-identity]")
            : "[no-network]";

        Debug.Log($"{who} {msg}");
    }
}
