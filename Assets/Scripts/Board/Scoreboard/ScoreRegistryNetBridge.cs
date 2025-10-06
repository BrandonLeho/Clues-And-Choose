/* using Mirror;
using UnityEngine;

public class ScoreRegistryNetBridge : NetworkBehaviour
{
    public static ScoreRegistryNetBridge Instance;

    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        ScoreRegistry.OnScoreChanged += ServerForwardScoreChanged;
    }

    public override void OnStopServer()
    {
        ScoreRegistry.OnScoreChanged -= ServerForwardScoreChanged;
    }

    [Server]
    void ServerForwardScoreChanged(string name, int newScore)
    {
        RpcApplyScore(name, newScore);
    }

    [ClientRpc]
    void RpcApplyScore(string name, int newScore)
    {
        ScoreRegistry.SetScore(name, newScore);
    }
}
 */