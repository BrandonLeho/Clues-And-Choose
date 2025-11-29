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

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.AddListener(HandleRoundChanged);
            RoundManager.Instance.onClueGiverChangedClient.AddListener(HandleClueGiverChanged);
        }

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

    void HandlePlacerChanged(uint newPlacerNetId)
    {
        if (debugLogs)
        {
            string name = ResolvePlacerName(newPlacerNetId);
            Log($"[Timer] PlacerChanged → NetId={newPlacerNetId}, Name={name}");
        }

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

    void HandleRoundChanged(int _, uint __)
    {
        if (debugLogs)
            Log("[Timer] RoundChanged → stop & reset timer.");
        StopTimer();
    }

    void HandleClueGiverChanged(uint ___)
    {
        if (debugLogs)
            Log("[Timer] ClueGiverChanged → stop & reset timer.");
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
            string name = ResolvePlacerName(tm.currentPlacerNetId);
            Log($"[Timer] START → Name={name}, NetId={tm.currentPlacerNetId}, isLocalTurn={isLocalTurn}");
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
        bool isLocalTurn = CoinPlacementTurnManager.IsLocalPlayersTurn();

        if (isLocalTurn)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → local player's turn, forcing coin drop if dragging (local).");

            CoinDragHandler.ForceDropIfDragging();
        }
        else if (debugLogs)
        {
            Log("[Timer] OnTimerExpired → not local player's turn (local check).");
        }

        if (!NetworkServer.active)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → not server, skipping forced advance.");
            return;
        }

        var tm = CoinPlacementTurnManager.Instance;
        if (!tm)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → no CoinPlacementTurnManager on server.");
            return;
        }

        uint placerAtExpiry = tm.currentPlacerNetId;
        if (placerAtExpiry == 0)
        {
            if (debugLogs)
                Log("[Timer] OnTimerExpired → no active placer on server.");
            return;
        }

        tm.ServerForceDropOnCurrentPlacer();

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
            if (debugLogs)
                Log("[Timer] Co_ServerAdvanceAfterDelay → placer became 0, nothing to do.");
            yield break;
        }

        string name = ResolvePlacerName(placerAtExpiry);
        Log($"[Timer] Co_ServerAdvanceAfterDelay → forcing advance → Name={name}, NetId={placerAtExpiry}");


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
