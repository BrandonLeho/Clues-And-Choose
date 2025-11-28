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
        if (newPlacerNetId == 0)
        {
            StopTimer();
            return;
        }

        TryStartTimerIfReady();
    }

    void HandleTargetChosen(int col, int row, Color color)
    {
        TryStartTimerIfReady();
    }

    void HandleRoundDecision(bool endNow)
    {
        StopTimer();
    }

    void TryStartTimerIfReady()
    {
        var pc = PhaseController.Instance;
        var tm = CoinPlacementTurnManager.Instance;

        if (!pc || !tm)
        {
            StopTimer();
            return;
        }

        bool hasTarget = pc.ClientHasTarget;
        bool hasPlacer = tm.currentPlacerNetId != 0;

        if (!hasTarget || !hasPlacer)
        {
            StopTimer();
            return;
        }

        _remaining = turnDurationSeconds;
        _running = true;
        UpdateLabel(active: true, remainingSeconds: _remaining);
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
    }

    void OnTimerExpired()
    {
        if (!CoinPlacementTurnManager.IsLocalPlayersTurn())
            return;
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
}
