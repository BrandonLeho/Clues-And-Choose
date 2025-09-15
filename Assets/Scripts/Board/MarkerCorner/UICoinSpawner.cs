using Mirror;
using UnityEngine;

public class UiCoinSpawner : NetworkBehaviour
{
    [SerializeField] GameObject coinPrefab;

    [Server]
    public void SpawnCoinFor(NetworkConnectionToClient ownerConn)
    {
        var coin = Instantiate(coinPrefab);
        NetworkServer.Spawn(coin, ownerConn);
    }
}
