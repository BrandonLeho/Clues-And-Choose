using Mirror;
using UnityEngine;

public sealed class EndRoundPromptChoiceBridge : MonoBehaviour
{
    void OnEnable() => EndRoundPromptUI.OnChoiceDecided += HandleChoice;
    void OnDisable() => EndRoundPromptUI.OnChoiceDecided -= HandleChoice;

    void HandleChoice(EndRoundOptionHover.OptionKind choice)
    {
        var ctrl = PhaseController.Instance;
        if (!ctrl) return;
        bool endNow = choice == EndRoundOptionHover.OptionKind.Yes;
        ctrl.CmdClueGiverChoose(endNow);
    }
}
