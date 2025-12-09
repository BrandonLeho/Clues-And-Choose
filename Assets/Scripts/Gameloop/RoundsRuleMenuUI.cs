using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class RoundsRuleMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Button decreaseButton;
    [SerializeField] Button increaseButton;
    [SerializeField] TextMeshProUGUI valueLabel;

    [Header("Config")]
    [SerializeField, Min(1)] int stepRounds = 1;
    [SerializeField, Min(1)] int minRounds = 1;
    [SerializeField, Min(1)] int maxRounds = 100;
    [SerializeField] string suffix = " Rounds";

    void OnEnable()
    {
        if (decreaseButton) decreaseButton.onClick.AddListener(OnDecreaseClicked);
        if (increaseButton) increaseButton.onClick.AddListener(OnIncreaseClicked);

        GameRuleSettings.OnMaxFullCyclesChanged -= HandleMaxFullCyclesChanged;
        GameRuleSettings.OnMaxFullCyclesChanged += HandleMaxFullCyclesChanged;

        RefreshFromRules();
        RefreshInteractable();
    }

    void OnDisable()
    {
        if (decreaseButton) decreaseButton.onClick.RemoveListener(OnDecreaseClicked);
        if (increaseButton) increaseButton.onClick.RemoveListener(OnIncreaseClicked);

        GameRuleSettings.OnMaxFullCyclesChanged -= HandleMaxFullCyclesChanged;
    }

    void Update()
    {
        RefreshInteractable();
    }

    void RefreshFromRules()
    {
        int rounds = GameRuleSettings.CurrentMaxFullCycles;
        HandleMaxFullCyclesChanged(rounds);
    }

    void HandleMaxFullCyclesChanged(int rounds)
    {
        rounds = Mathf.Clamp(rounds, minRounds, maxRounds);
        if (valueLabel)
        {
            valueLabel.text = $"{rounds}{suffix}";
        }
    }

    void OnDecreaseClicked() => TryAdjust(-stepRounds);
    void OnIncreaseClicked() => TryAdjust(+stepRounds);

    void TryAdjust(int delta)
    {
        var rules = GameRuleSettings.Instance;
        if (!rules) return;

        if (!NetworkServer.active)
        {
            Debug.LogWarning("[RoundsRuleMenuUI] Only the host can change round rules.");
            return;
        }

        int current = GameRuleSettings.CurrentMaxFullCycles;
        int next = Mathf.Clamp(current + delta, minRounds, maxRounds);

        rules.CmdSetMaxFullCycles(next);
    }

    void RefreshInteractable()
    {
        bool hostOrOffline = !NetworkClient.active || NetworkServer.active;

        if (decreaseButton) decreaseButton.interactable = hostOrOffline;
        if (increaseButton) increaseButton.interactable = hostOrOffline;
    }
}