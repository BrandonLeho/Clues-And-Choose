using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class RouletteNetSync : NetworkBehaviour
{
    [Header("Scene refs")]
    [SerializeField] RouletteText roulette;

    [Header("Spin config (applied to all clients)")]
    [SerializeField] float initialSpeed = 1200f;
    [SerializeField] float decelMultiplier = 1.0f;
    [SerializeField] int extraLoops = 3;
    [SerializeField] float maxSlowdownSeconds = 0f;

    [Header("Data broadcast")]
    [Tooltip("Send the names to clients in the RPC (most robust). Turn off only if every client already has the same list/order.")]
    [SerializeField] bool sendNamesInRpc = true;

    [Header("Timing")]
    [Tooltip("Small delay so all clients begin at the same NetworkTime.")]
    [SerializeField] float startDelay = 0.10f;

    bool spinScheduled;
    double scheduledStart;

    void Reset() => roulette = GetComponent<RouletteText>();

    public void RequestSpin()
    {
        if (isServer) ServerStartSpin();
        else CmdRequestSpin();
    }

    [Command(requiresAuthority = false)]
    void CmdRequestSpin() => ServerStartSpin();

    [Server]
    void ServerStartSpin()
    {
        var names = GetEntriesServer();
        if (names == null || names.Count == 0)
        {
            Debug.LogWarning("RouletteNetSync: No names to spin.");
            return;
        }

        int chosenIndex = Random.Range(0, names.Count);
        int loops = Mathf.Max(0, extraLoops);
        double startTime = NetworkTime.time + startDelay;

        if (sendNamesInRpc)
            RpcStartSpinWithNames(names.ToArray(), chosenIndex, loops, initialSpeed, decelMultiplier, maxSlowdownSeconds, startTime);
        else
            RpcStartSpin(chosenIndex, loops, initialSpeed, decelMultiplier, maxSlowdownSeconds, startTime);
    }

    [ClientRpc]
    void RpcStartSpinWithNames(string[] names, int chosenIndex, int loops, float initSpeed, float decelMult, float maxSlowSec, double startTime)
    {
        if (!roulette) return;
        ApplyEntries(names);
        ApplySpinConfig(chosenIndex, loops, initSpeed, decelMult, maxSlowSec);
        Schedule(startTime);
    }

    [ClientRpc]
    void RpcStartSpin(int chosenIndex, int loops, float initSpeed, float decelMult, float maxSlowSec, double startTime)
    {
        if (!roulette) return;
        ApplySpinConfig(chosenIndex, loops, initSpeed, decelMult, maxSlowSec);
        Schedule(startTime);
    }

    void ApplyEntries(IList<string> names)
    {
        roulette.entries = new List<string>(names);
        roulette.Rebuild();
    }

    void ApplySpinConfig(int index, int loops, float initSpeed, float decelMult, float maxSlowSec)
    {
        roulette.forceTargetIndex = index;
        roulette.minExtraLoops = loops;
        roulette.maxExtraLoops = loops;
        roulette.initialSpeed = initSpeed;
        roulette.decelMultiplier = decelMult;
        roulette.maxSlowdownSeconds = maxSlowSec;
    }

    void Schedule(double networkStartTime)
    {
        roulette.Rebuild();
        scheduledStart = networkStartTime;
        spinScheduled = true;
    }

    void Update()
    {
        if (spinScheduled && NetworkTime.time >= scheduledStart)
        {
            spinScheduled = false;
            roulette.StartSpin();
        }
    }

    List<string> GetEntriesServer()
    {
        if (RosterStore.Instance != null && RosterStore.Instance.Names != null && RosterStore.Instance.Names.Count > 0)
            return new List<string>(RosterStore.Instance.Names);

        return null;
    }
}
