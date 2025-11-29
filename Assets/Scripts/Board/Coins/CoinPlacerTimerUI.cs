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
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;

        CoinRoundLockManager.OnUnlocked -= HandleCoinsUnlocked;
        CoinRoundLockManager.OnUnlocked += HandleCoinsUnlocked;

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
        CoinRoundLockManager.OnUnlocked -= HandleCoinsUnlocked;
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

    void HandleCoinsUnlocked()
    {
        TryStartTimerIfReady();
    }

    void TryStartTimerIfReady()
    {
        var tm = CoinPlacementTurnManager.Instance;
        if (!tm || tm.currentPlacerNetId == 0)
        {
            StopTimer();
            return;
        }

        var pc = PhaseController.Instance;
        bool hasTarget = pc && pc.ClientHasTarget;

        bool coinsUnlocked = !CoinRoundLockManager.IsLocked;

        if (!hasTarget || !coinsUnlocked)
        {
            return;
        }

        _remaining = turnDurationSeconds;
        _running = true;
        UpdateLabel(active: true, remainingSeconds: _remaining);

        if (debugLogs)
        {
            string placerName = ResolvePlacerName(tm.currentPlacerNetId);
            Log($"[Timer] TryStartTimerIfReady → START for placer " +
                $"{placerName} ({tm.currentPlacerNetId}), duration={turnDurationSeconds}s");
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
        bool isLocalTurn = CoinPlacementTurnManager.IsLocalPlayersTurn();

        if (isLocalTurn)
        {
            CoinDragHandler.ForceDropIfDragging();
        }

        if (!NetworkServer.active)
        {
            return;
        }

        var tm = CoinPlacementTurnManager.Instance;
        if (!tm)
        {
            return;
        }

        uint placerAtExpiry = tm.currentPlacerNetId;
        if (placerAtExpiry == 0)
        {
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
