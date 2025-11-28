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

        UpdateLabel(active: false, remainingSeconds: 0f);

        var tm = CoinPlacementTurnManager.Instance;
        if (tm != null)
        {
            HandlePlacerChanged(tm.currentPlacerNetId);
        }
        else
        {
            TryStartTimerIfReady();
        }
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientTargetChosen -= HandleTargetChosen;
        PhaseController.OnClientRoundDecision -= HandleRoundDecision;
    }

    void Update()
    {
        if (!_running) return;

        _remaining -= Time.unscaledDeltaTime;

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            Log("[Timer] Expired → attempting auto-drop.");
            OnTimerExpired();
            StopTimer();
        }
        else
        {
            UpdateLabel(active: true, remainingSeconds: _remaining);
        }
    }

    void HandlePlacerChanged(uint newPlacerNetId)
    {
        if (debugLogs)
            Log($"[Timer] PlacerChanged → newPlacerNetId={newPlacerNetId}");

        if (newPlacerNetId == 0)
        {
            StopTimer();
            return;
        }

        TryStartTimerIfReady();
    }

    void HandleTargetChosen(int col, int row, Color color)
    {
        if (debugLogs)
            Log($"[Timer] TargetChosen → col={col}, row={row}");

        TryStartTimerIfReady();
    }

    void HandleRoundDecision(bool endNow)
    {
        if (debugLogs)
            Log($"[Timer] RoundDecision → endNow={endNow} (stop timer).");

        StopTimer();
    }

    void TryStartTimerIfReady()
    {
        var pc = PhaseController.Instance;
        var tm = CoinPlacementTurnManager.Instance;

        if (!pc || !tm)
        {
            Log("[Timer] TryStartTimerIfReady → missing PhaseController or CoinPlacementTurnManager.");
            StopTimer();
            return;
        }

        bool hasTarget = pc.ClientHasTarget;
        bool hasPlacer = tm.currentPlacerNetId != 0;

        if (debugLogs)
            Log($"[Timer] TryStartTimerIfReady → hasTarget={hasTarget}, hasPlacer={hasPlacer}");

        if (!hasTarget || !hasPlacer)
        {
            StopTimer();
            return;
        }

        _remaining = turnDurationSeconds;
        _running = true;
        UpdateLabel(active: true, remainingSeconds: _remaining);

        if (debugLogs)
        {
            bool isLocalTurn = CoinPlacementTurnManager.IsLocalPlayersTurn();
            Log($"[Timer] START for placerNetId={tm.currentPlacerNetId}, isLocalTurn={isLocalTurn}");
        }
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
        if (!CoinPlacementTurnManager.IsLocalPlayersTurn())
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → not local player's turn, ignoring.");
            return;
        }

        if (debugLogs)
            Log("[Timer] OnTimerExpired → forcing coin drop if dragging.");

        CoinDragHandler.ForceDropIfDragging();
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
