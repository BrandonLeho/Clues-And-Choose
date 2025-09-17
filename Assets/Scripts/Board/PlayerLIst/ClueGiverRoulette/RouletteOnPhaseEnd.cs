using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RouletteOnPhaseEnd : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ColorChoosingPhaseController phase;
    [SerializeField] private ClueGiverRoulette roulette;

    [Header("Timing")]
    [Tooltip("Extra delay AFTER the color choosing phase finishes, before starting the roulette.")]
    [SerializeField] private float delayAfterPhaseEnd = 1.0f;

    [Header("Names Source")]
    [Tooltip("If true, pull names from RosterStore.Instance.Names; otherwise use Names (below).")]
    [SerializeField] private bool pullFromRosterStore = true;
    [SerializeField] private List<string> names = new();

    [Header("Hooking")]
    [Tooltip("Automatically subscribes to phase.onPhaseEnded on enable.")]
    [SerializeField] private bool autoHookPhaseEvent = true;

    bool _queuedOnce;

    void Reset()
    {
        if (!phase) phase = FindFirstObjectByType<ColorChoosingPhaseController>(FindObjectsInactive.Include);
        if (!roulette) roulette = FindFirstObjectByType<ClueGiverRoulette>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        if (autoHookPhaseEvent && phase != null)
            phase.onPhaseEnded.AddListener(TriggerStartRouletteFromPhaseEnd);
    }

    void OnDisable()
    {
        if (autoHookPhaseEvent && phase != null)
            phase.onPhaseEnded.RemoveListener(TriggerStartRouletteFromPhaseEnd);
    }

    public void TriggerStartRouletteFromPhaseEnd()
    {
        if (_queuedOnce || !isActiveAndEnabled) return;
        StartCoroutine(Co_DelayAndStart());
    }

    IEnumerator Co_DelayAndStart()
    {
        _queuedOnce = true;

        if (delayAfterPhaseEnd > 0f)
            yield return new WaitForSecondsRealtime(delayAfterPhaseEnd);

        var list = pullFromRosterStore && RosterStore.Instance != null
            ? RosterStore.Instance.Names
            : names;

        if (list == null || list.Count == 0)
        {
            Debug.LogWarning("[RouletteOnPhaseEnd] No names available to start the roulette.");
            yield break;
        }

        roulette.BuildFromNames(list);
        roulette.StartRoulette();
    }
}
