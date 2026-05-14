using UnityEngine;

[DisallowMultipleComponent]
public class RouletteModeObjectGate : MonoBehaviour
{
    [SerializeField] GameObject target;

    void Reset()
    {
        target = gameObject;
    }

    void OnEnable()
    {
        GameRuleSettings.OnRouletteModeChanged += ApplyRouletteMode;
        ApplyCurrentRuleState();
    }

    void Start()
    {
        ApplyCurrentRuleState();
    }

    void Update()
    {
        ApplyCurrentRuleState();
    }

    void OnDisable()
    {
        GameRuleSettings.OnRouletteModeChanged -= ApplyRouletteMode;
    }

    void ApplyCurrentRuleState()
    {
        ApplyRouletteMode(GameRuleSettings.IsRouletteModeEnabled);
    }

    void ApplyRouletteMode(bool enabled)
    {
        if (target && target.activeSelf != enabled)
            target.SetActive(enabled);
    }
}
