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

    bool _placingPhaseActive;

    bool IsSimultaneousMode() => !GameRuleSettings.IsLockAllEnabled;

    void Awake()
    {
        if (!timeLabelUI) timeLabelUI = GetComponent<Text>();
        if (!timeLabelTMP) timeLabelTMP = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;

        PhaseController.OnClientPlacingPhaseStarted -= HandlePlacingPhaseStarted;
        PhaseController.OnClientPlacingPhaseStarted += HandlePlacingPhaseStarted;
        PhaseController.OnClientPlacingPhaseEnded -= HandlePlacingPhaseEnded;
        PhaseController.OnClientPlacingPhaseEnded += HandlePlacingPhaseEnded;

        _placingPhaseActive = PhaseController.ClientPlacingPhaseActive;

        GameRuleSettings.OnTurnDurationChanged -= HandleRuleTurnDurationChanged;
        GameRuleSettings.OnTurnDurationChanged += HandleRuleTurnDurationChanged;
        ApplyRuleTurnDuration();

        UpdateLabel(active: false, remainingSeconds: 0f);

        var tm = CoinPlacementTurnManager.Instance;
        if (tm != null)
        {
            HandlePlacerChanged(tm.currentPlacerNetId);
        }
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientPlacingPhaseStarted -= HandlePlacingPhaseStarted;
        PhaseController.OnClientPlacingPhaseEnded -= HandlePlacingPhaseEnded;
        GameRuleSettings.OnTurnDurationChanged -= HandleRuleTurnDurationChanged;
    }

    void Update()
    {
        if (!_running) return;

        _remaining -= Time.unscaledDeltaTime;

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            OnTimerExpired();
            StopTimer();
        }
        else
        {
            UpdateLabel(active: true, remainingSeconds: _remaining);
        }
    }

    void ApplyRuleTurnDuration()
    {
        if (GameRuleSettings.Instance != null)
        {
            float ruleSeconds = GameRuleSettings.CurrentTurnDurationSeconds;
            turnDurationSeconds = Mathf.Max(0.5f, ruleSeconds);
        }
    }

    void HandleRuleTurnDurationChanged(float newSeconds)
    {
        turnDurationSeconds = Mathf.Max(0.5f, newSeconds);

        if (_running)
        {
            _remaining = Mathf.Min(_remaining, turnDurationSeconds);
            UpdateLabel(active: true, remainingSeconds: _remaining);
        }

        if (debugLogs)
            Log($"[Timer] Rule duration updated → {turnDurationSeconds}s");
    }

    void HandlePlacerChanged(uint newPlacerNetId)
    {
        if (debugLogs)
        {
            string name = ResolvePlacerName(newPlacerNetId);
            Log($"[Timer] PlacerChanged → NetId={newPlacerNetId}, Name={name}, placingPhaseActive={_placingPhaseActive}");
        }

        if (IsSimultaneousMode())
        {
            if (_placingPhaseActive)
                TryStartTimerIfReady();
            else
                StopTimer();
            return;
        }

        if (newPlacerNetId == 0 || !_placingPhaseActive)
        {
            StopTimer();
            return;
        }
        TryStartTimerIfReady();
    }

    void HandlePlacingPhaseStarted()
    {
        _placingPhaseActive = true;

        if (debugLogs)
            Log("[Timer] Placing phase STARTED → checking if timer should start.");

        if (IsSimultaneousMode())
        {
            TryStartTimerIfReady();
        }
        else
        {
            TryStartTimerIfReady();
        }
    }

    void HandlePlacingPhaseEnded()
    {
        _placingPhaseActive = false;

        if (debugLogs)
            Log("[Timer] Placing phase ENDED → stopping timer.");

        StopTimer();
    }

    void TryStartTimerIfReady()
    {
        var tm = CoinPlacementTurnManager.Instance;

        if (IsSimultaneousMode())
        {
            if (!_placingPhaseActive)
            {
                StopTimer();
                return;
            }

            ApplyRuleTurnDuration();
            _remaining = turnDurationSeconds;
            _running = true;
            UpdateLabel(true, _remaining);
            return;
        }

        if (!tm || tm.currentPlacerNetId == 0 || !_placingPhaseActive)
        {
            StopTimer();
            return;
        }

        ApplyRuleTurnDuration();

        _remaining = turnDurationSeconds;
        _running = true;
        UpdateLabel(active: true, remainingSeconds: _remaining);

        if (debugLogs)
        {
            string placerName = ResolvePlacerName(tm.currentPlacerNetId);
            Log($"[Timer] TryStartTimerIfReady → START for placer {placerName} ({tm.currentPlacerNetId}), duration={turnDurationSeconds}s");
        }
    }

    void StopTimer()
    {
        if (!_running)
        {
            UpdateLabel(active: false, remainingSeconds: 0f);
            return;
        }

        _running = false;
        UpdateLabel(active: false, remainingSeconds: 0f);

        if (debugLogs)
            Log("[Timer] StopTimer → timer stopped and hidden.");
    }

    void OnTimerExpired()
    {
        if (IsSimultaneousMode())
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → simultaneous mode, forcing drop for all players.");

            CoinDragHandler.ForceDropIfDragging();

            if (NetworkServer.active)
            {
                var tm = CoinPlacementTurnManager.Instance;
                if (tm != null)
                {
                    tm.ServerForceDropAllSimultaneousPlayers();
                }
            }

            return;
        }

        bool isLocalTurn = CoinPlacementTurnManager.IsLocalPlayersTurn();

        if (isLocalTurn)
        {
            CoinDragHandler.ForceDropIfDragging();
        }

        if (!NetworkServer.active)
        {
            return;
        }

        var tmTurn = CoinPlacementTurnManager.Instance;
        if (!tmTurn)
        {
            return;
        }

        uint placerAtExpiry = tmTurn.currentPlacerNetId;
        if (placerAtExpiry == 0)
        {
            return;
        }

        tmTurn.ServerForceDropOnCurrentPlacer();

        if (debugLogs)
        {
            string name = ResolvePlacerName(placerAtExpiry);
            Log($"[Timer] OnTimerExpired → scheduling forced advance → Name={name}, NetId={placerAtExpiry}");
        }

        StartCoroutine(Co_ServerAdvanceAfterDelay(placerAtExpiry));
    }

    System.Collections.IEnumerator Co_ServerAdvanceAfterDelay(uint placerAtExpiry)
    {
        yield return new WaitForSeconds(0.25f);

        if (!NetworkServer.active)
            yield break;

        var tm = CoinPlacementTurnManager.Instance;
        if (!tm)
            yield break;

        if (tm.currentPlacerNetId != placerAtExpiry)
        {
            if (debugLogs)
                Log($"[Timer] Co_ServerAdvanceAfterDelay → placer changed from {placerAtExpiry} to {tm.currentPlacerNetId}, skipping forced advance.");
            yield break;
        }

        if (placerAtExpiry == 0)
        {
            yield break;
        }

        tm.ServerNoteSuccessfulPlacement(placerAtExpiry);
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

    string ResolvePlacerName(uint netId)
    {
        if (netId == 0) return "<none>";

        if (RosterStore.TryGetNameByNetId(netId, out var name))
            return name;

        return $"NetId:{netId}";
    }
}
