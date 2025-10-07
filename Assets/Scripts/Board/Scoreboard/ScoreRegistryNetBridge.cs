using UnityEngine;
using Mirror;

public class ScoreRegistryNetBridge : NetworkBehaviour
{
    public override void OnStartServer()
    {
        ScoreRegistry.OnScoreChanged += HandleServerScoreChanged;
    }

    public override void OnStopServer()
    {
        ScoreRegistry.OnScoreChanged -= HandleServerScoreChanged;
    }

    [Server]
    void HandleServerScoreChanged(string name, int newScore)
    {
        if (ScoreRegistry.IsNetworkApplying) return;
        Debug.Log("Player [" + name + "] is awarded: " + newScore + " points");
        RpcPushScore(name, newScore);
    }

    [ClientRpc]
    void RpcPushScore(string name, int newScore)
    {
        ScoreRegistry.SetScoreFromNetwork(name, newScore);
    }

    [ClientRpc]
    void RpcAddScore(string name, int delta)
    {
        ScoreRegistry.AddScoreFromNetwork(name, delta);
    }

    [TargetRpc]
    public void TargetSyncAllScores(NetworkConnection target)
    {
        foreach (var kv in ScoreRegistry.GetAll())
            ScoreRegistry.SetScoreFromNetwork(kv.Key, kv.Value);
    }
}
