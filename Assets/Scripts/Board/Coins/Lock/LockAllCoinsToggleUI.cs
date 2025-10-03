using UnityEngine;
using UnityEngine.UI;

public class LockAllCoinsToggleUI : MonoBehaviour
{
    [SerializeField] Toggle toggle;

    void Awake()
    {
        if (!toggle) toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnChanged);
    }

    void Start()
    {
        toggle.isOn = true;
    }

    void OnEnable()
    {
        if (GameRuleSettings.Instance)
            toggle.SetIsOnWithoutNotify(GameRuleSettings.Instance.lockAllCoinsEnabled);
    }

    void OnChanged(bool isOn)
    {
        if (GameRuleSettings.Instance)
            GameRuleSettings.Instance.CmdSetLockAllCoinsEnabled(isOn);
    }
}
