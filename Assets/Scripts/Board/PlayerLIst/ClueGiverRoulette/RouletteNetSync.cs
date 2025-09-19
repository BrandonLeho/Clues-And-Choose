using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RouletteNetSync : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] RouletteText roulette;

    [Header("Roster")]
    [SerializeField] bool useRosterStore = true;
    [SerializeField] bool sortNames = true;

    [Header("Spin Scheduling")]
    [SerializeField] float startDelay = 0.35f; // seconds from now (server) to start on clients

    bool spinning;

    void Reset() => roulette = GetComponent<RouletteText>();
    void Awake() { if (!roulette) roulette = GetComponent<RouletteText>(); }

    public override void OnStartClient()
    {
        base.OnStartClient();
        BuildEntries();
    }

    public void BuildEntries()
    {
        if (!roulette) return;
        var names = new List<string>();

        if (useRosterStore && RosterStore.Instance != null && RosterStore.Instance.Names != null)
            names.AddRange(RosterStore.Instance.Names);

        if (sortNames) names.Sort(StringComparer.InvariantCultureIgnoreCase);

        if (names.Count > 0)
        {
            roulette.entries = names;
            roulette.Rebuild();
        }
    }

    [Client]
    public void RequestSpin()
    {
        if (spinning) return;
        CmdRequestSpin();
    }

    [Command(requiresAuthority = false)]
    void CmdRequestSpin()
    {
        if (spinning) return;
        spinning = true;

        var names = (RosterStore.Instance != null ? RosterStore.Instance.Names : null) ?? new List<string>();
        if (sortNames) names.Sort(StringComparer.InvariantCultureIgnoreCase);
        int count = names.Count;

        if (count == 0) { spinning = false; return; }

        int chosenIndex = UnityEngine.Random.Range(0, count);

        int loops = UnityEngine.Random.Range(roulette.minExtraLoops, roulette.maxExtraLoops + 1);

        float speed = roulette.initialSpeed;
        float decel = roulette.decelMultiplier;

        double startAt = NetworkTime.time + startDelay;

        RpcStartSpin(chosenIndex, loops, startAt, speed, decel);
    }

    [ClientRpc]
    void RpcStartSpin(int chosenIndex, int loops, double startNetworkTime, float speed, float decel)
    {
        StartCoroutine(CoSpin(chosenIndex, loops, startNetworkTime, speed, decel));
    }

    IEnumerator CoSpin(int chosenIndex, int loops, double startNetworkTime, float speed, float decel)
    {
        if (roulette.entries == null || roulette.entries.Count == 0) BuildEntries();

        roulette.forceTargetIndex = chosenIndex;
        roulette.minExtraLoops = loops;
        roulette.maxExtraLoops = loops;
        roulette.initialSpeed = speed;
        roulette.decelMultiplier = decel;
        roulette.Rebuild();

        while (NetworkTime.time < startNetworkTime - 0.001) yield return null;

        spinning = true;
        roulette.OnSpinComplete.RemoveListener(OnSpinCompleteLocal);
        roulette.OnSpinComplete.AddListener(OnSpinCompleteLocal);
        roulette.StartSpin();
    }

    void OnSpinCompleteLocal(string name, int index)
    {
        spinning = false;
    }
}
