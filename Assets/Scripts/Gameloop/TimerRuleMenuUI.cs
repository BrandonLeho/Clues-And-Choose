using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class TimerRuleMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Button decreaseButton;
    [SerializeField] Button increaseButton;
    [SerializeField] TextMeshProUGUI valueLabel;

    [Header("Config")]
    [SerializeField, Min(0.5f)] float stepSeconds = 5f;
    [SerializeField, Min(0.5f)] float minSeconds = 5f;
    [SerializeField, Min(0.5f)] float maxSeconds = 120f;
    [SerializeField] string suffix = "s";

    void OnEnable()
    {
        if (decreaseButton) decreaseButton.onClick.AddListener(OnDecreaseClicked);
        if (increaseButton) increaseButton.onClick.AddListener(OnIncreaseClicked);

        GameRuleSettings.OnTurnDurationChanged -= HandleTurnDurationChanged;
        GameRuleSettings.OnTurnDurationChanged += HandleTurnDurationChanged;

        RefreshFromRules();
        RefreshInteractable();
    }

    void OnDisable()
    {
        if (decreaseButton) decreaseButton.onClick.RemoveListener(OnDecreaseClicked);
        if (increaseButton) increaseButton.onClick.RemoveListener(OnIncreaseClicked);

        GameRuleSettings.OnTurnDurationChanged -= HandleTurnDurationChanged;
    }

    void Update()
    {
        RefreshInteractable();
    }

    void RefreshFromRules()
    {
        float seconds = GameRuleSettings.CurrentTurnDurationSeconds;
        HandleTurnDurationChanged(seconds);
    }

    void HandleTurnDurationChanged(float seconds)
    {
        seconds = Mathf.Clamp(seconds, minSeconds, maxSeconds);
        if (valueLabel)
        {
            valueLabel.text = $"{Mathf.RoundToInt(seconds)}{suffix}";
        }
    }

    void OnDecreaseClicked() => TryAdjust(-stepSeconds);
    void OnIncreaseClicked() => TryAdjust(+stepSeconds);

    void TryAdjust(float delta)
    {
        var rules = GameRuleSettings.Instance;
        if (!rules) return;

        if (!NetworkServer.active)
        {
            Debug.LogWarning("[TimerRuleMenuUI] Only the host can change timer rules.");
            return;
        }

        float current = GameRuleSettings.CurrentTurnDurationSeconds;
        float next = Mathf.Clamp(current + delta, minSeconds, maxSeconds);

        rules.CmdSetTurnDurationSeconds(next);
    }

    void RefreshInteractable()
    {
        bool hostOrOffline = !NetworkClient.active || NetworkServer.active;

        if (decreaseButton) decreaseButton.interactable = hostOrOffline;
        if (increaseButton) increaseButton.interactable = hostOrOffline;
    }
}
