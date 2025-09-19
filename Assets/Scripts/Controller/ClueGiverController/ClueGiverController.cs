using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class ClueGiverManager : NetworkBehaviour
{
    [Header("Order Source")]
    [Tooltip("Match this to RouletteNetSync.sortNames so both use the same ordering.")]
    [SerializeField] bool sortNames = true;

    [Header("Behavior")]
    [Tooltip("If the current name disappears (e.g., player left), auto-advance to the next available.")]
    [SerializeField] bool autoSkipMissing = true;

    [Header("Events (Client)")]
    public UnityEvent<string, int, int> OnClueGiverChanged;

    [SyncVar(hook = nameof(OnIndexChanged))]
    int currentIndex = -1;

    [SyncVar(hook = nameof(OnRoundChanged))]
    int roundNumber = 0;

    readonly List<string> order = new List<string>();

    bool initialChosen = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        BuildOrderFromRoster();

        if (currentIndex < 0 && order.Count > 0)
        {
            currentIndex = 0;
            roundNumber = 1;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        BuildOrderFromRoster();
        InvokeClientEvent();
    }

    [Server]
    public void ServerSetInitialByWinner(string winnerName)
    {
        if (initialChosen) return;
        BuildOrderFromRoster();
        int idx = IndexOfName(order, winnerName);
        if (idx < 0 && order.Count > 0) idx = 0;
        currentIndex = idx;
        roundNumber = 1;
        initialChosen = true;
    }

    [Server]
    public void NextClueGiver()
    {
        BuildOrderFromRoster();
        if (order.Count == 0) { currentIndex = -1; return; }

        int start = Mathf.Clamp(currentIndex, 0, order.Count - 1);
        int idx = start;
        for (int step = 0; step < order.Count; step++)
        {
            idx = (idx + 1) % order.Count;
            if (!string.IsNullOrWhiteSpace(order[idx]))
            {
                currentIndex = idx;
                roundNumber += 1;
                return;
            }
        }
    }

    [Server]
    public void RefreshFromRoster()
    {
        string currentName = GetCurrentName();
        BuildOrderFromRoster();

        if (order.Count == 0)
        {
            currentIndex = -1;
            return;
        }

        if (!string.IsNullOrEmpty(currentName))
        {
            int idx = IndexOfName(order, currentName);
            if (idx >= 0)
            {
                currentIndex = idx;
                return;
            }
        }

        if (autoSkipMissing && order.Count > 0)
        {
            currentIndex = Mathf.Clamp(currentIndex, 0, order.Count - 1);
        }
        else
        {
            currentIndex = 0;
        }
    }

    public string GetCurrentName()
    {
        if (order.Count == 0 || currentIndex < 0 || currentIndex >= order.Count) return null;
        return order[currentIndex];
    }

    public int GetCurrentIndex() => currentIndex;
    public int GetRoundNumber() => roundNumber;

    void OnIndexChanged(int _, int __)
    {
        if (isClient) BuildOrderFromRoster();
        InvokeClientEvent();
    }

    void OnRoundChanged(int _, int __)
    {
        InvokeClientEvent();
    }

    void InvokeClientEvent()
    {
        if (OnClueGiverChanged == null) return;
        string name = GetCurrentName();
        OnClueGiverChanged.Invoke(name, currentIndex, roundNumber);
    }

    void BuildOrderFromRoster()
    {
        order.Clear();
        var names = (RosterStore.Instance != null ? RosterStore.Instance.Names : null);
        if (names != null)
        {
            order.AddRange(names);
            if (sortNames) order.Sort(StringComparer.InvariantCultureIgnoreCase);
        }

        if (order.Count == 0) { currentIndex = -1; return; }
        currentIndex = Mathf.Clamp(currentIndex, -1, order.Count - 1);

        if (autoSkipMissing && currentIndex >= 0)
        {
            string name = GetCurrentName();
            if (IndexOfName(order, name) < 0)
            {
                currentIndex = Mathf.Clamp(currentIndex, 0, order.Count - 1);
            }
        }
    }

    static int IndexOfName(List<string> list, string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i], name, StringComparison.Ordinal)) return i;
        return -1;
    }
}
