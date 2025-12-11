using UnityEngine;
using UnityEngine.UI;

public class RouletteModeToggleUI : MonoBehaviour
{
    [SerializeField] private Toggle rouletteToggle;

    void OnEnable()
    {
        if (!rouletteToggle)
            rouletteToggle = GetComponent<Toggle>();

        rouletteToggle.onValueChanged.AddListener(OnToggleValueChanged);

        if (GameRuleSettings.Instance)
        {
            rouletteToggle.SetIsOnWithoutNotify(GameRuleSettings.Instance.rouletteModeEnabled);
            GameRuleSettings.OnRouletteModeChanged += HandleRouletteModeChanged;
        }
    }

    void OnDisable()
    {
        if (rouletteToggle)
            rouletteToggle.onValueChanged.RemoveListener(OnToggleValueChanged);

        if (GameRuleSettings.Instance)
            GameRuleSettings.OnRouletteModeChanged -= HandleRouletteModeChanged;
    }

    void OnToggleValueChanged(bool value)
    {
        if (!GameRuleSettings.Instance)
        {
            Debug.LogWarning("RouletteModeToggleUI: No GameRuleSettings instance in scene.");
            return;
        }
        GameRuleSettings.Instance.CmdSetRouletteModeEnabled(value);
    }

    void HandleRouletteModeChanged(bool value)
    {
        if (rouletteToggle && rouletteToggle.isOn != value)
            rouletteToggle.SetIsOnWithoutNotify(value);
    }
}
