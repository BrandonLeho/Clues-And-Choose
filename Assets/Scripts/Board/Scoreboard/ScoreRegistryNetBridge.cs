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
        RpcPushScore(name, newScore);
    }

    [ClientRpc]
    void RpcPushScore(string name, int newScore)
    {
        ScoreRegistry.SetScore(name, newScore);
    }

    [TargetRpc]
    public void TargetSyncAllScores(NetworkConnection target)
    {
        foreach (var kv in ScoreRegistry.GetAll())
            ScoreRegistry.SetScore(kv.Key, kv.Value);
    }
}
