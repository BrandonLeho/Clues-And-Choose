using Mirror;
using UnityEngine;

public sealed class EndRoundPromptChoiceBridge : MonoBehaviour
{
    void OnEnable() => EndRoundPromptUI.OnChoiceDecided += HandleChoice;
    void OnDisable() => EndRoundPromptUI.OnChoiceDecided -= HandleChoice;

    void HandleChoice(EndRoundOptionHover.OptionKind choice)
    {
        Debug.Log("idusahgoa");
        var ctrl = PlacingPhaseController.Instance;
        if (!ctrl) return;
        bool endNow = choice == EndRoundOptionHover.OptionKind.Yes;
        ctrl.CmdClueGiverChoose(endNow);
    }
}
