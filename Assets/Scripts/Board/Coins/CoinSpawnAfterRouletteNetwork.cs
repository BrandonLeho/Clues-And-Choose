using UnityEngine;
using Mirror;

public class CoinSpawnAfterRouletteNetwork : NetworkBehaviour
{
    [SerializeField] CoinNetworkSpawner spawner;

    void Reset()
    {
        if (!spawner) spawner = FindFirstObjectByType<CoinNetworkSpawner>(FindObjectsInactive.Include);
    }

    public void TriggerSpawnAfterRoulette()
    {
        if (!isActiveAndEnabled) return;
        CmdRequestServerSlideSpawn();
    }

    [Command(requiresAuthority = false)]
    void CmdRequestServerSlideSpawn()
    {
        if (!spawner)
            spawner = FindFirstObjectByType<CoinNetworkSpawner>(FindObjectsInactive.Include);
        if (!spawner) { Debug.LogError("[CoinSpawnAfterRoulette] Spawner not found."); return; }

        spawner.ServerSpawnSlideIn();
    }
}
