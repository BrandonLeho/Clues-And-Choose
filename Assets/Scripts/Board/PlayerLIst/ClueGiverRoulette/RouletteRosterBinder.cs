using System.Collections.Generic;
using UnityEngine;

public class RouletteRosterBinder : MonoBehaviour
{
    [SerializeField] RouletteText roulette;
    [SerializeField] bool autoRebuildOnEnable = true;

    void Reset() => roulette = GetComponent<RouletteText>();

    void OnEnable()
    {
        if (autoRebuildOnEnable) RefreshFromRosterStore();
    }

    public void RefreshFromRosterStore()
    {
        if (RosterStore.Instance == null || RosterStore.Instance.Names == null) return;
        List<string> names = RosterStore.Instance.Names;
        if (names.Count == 0) return;

        roulette.entries = new List<string>(names);
        roulette.Rebuild();
        // roulette.StartSpin();
    }
}
