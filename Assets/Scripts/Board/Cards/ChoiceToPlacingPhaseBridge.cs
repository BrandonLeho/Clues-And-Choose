using UnityEngine;

public class ChoiceToPlacingPhaseBridge : MonoBehaviour
{
    [SerializeField] CardChoiceSelectController controller;

    void Reset()
    {
        if (!controller) controller = GetComponent<CardChoiceSelectController>();
    }

    void OnEnable()
    {
        if (!controller) controller = GetComponent<CardChoiceSelectController>();
        if (controller) controller.onChoiceSelected.AddListener(OnChoiceSelected);
    }

    void OnDisable()
    {
        if (controller) controller.onChoiceSelected.RemoveListener(OnChoiceSelected);
    }

    void OnChoiceSelected(CardChoiceSelectController.ChoicePayload _)
    {
        var phase = PlacingPhaseController.Instance;
        if (phase) phase.CmdStartPlacingPhase();
    }
}
