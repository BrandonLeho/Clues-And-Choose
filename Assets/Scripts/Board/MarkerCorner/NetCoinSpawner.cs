using System.Linq;
using Mirror;
using UnityEngine;

public class NetCoinSpawner : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] CoinSpawnGrid grid;
    [SerializeField] GameObject coinPrefab;

    [Header("Rules")]
    [Min(1)] public int coinsPerPlayer = 2;

    public override void OnStartServer()
    {
        base.OnStartServer();
        SpawnAllCoinsServer();
    }

    [Server]
    public void SpawnAllCoinsServer()
    {
        var registry = ColorLockRegistry.GetOrFind();
        if (!registry)
        {
            Debug.LogWarning("[NetCoinSpawner] No ColorLockRegistry found; aborting.");
            return;
        }

        var owners = registry.colorByOwner.Keys.ToList();
        owners.Sort((a, b) =>
        {
            int ia = registry.indexByOwner.TryGetValue(a, out var ai) ? ai : int.MaxValue;
            int ib = registry.indexByOwner.TryGetValue(b, out var bi) ? bi : int.MaxValue;
            return ia.CompareTo(ib);
        });

        int slotIdx = 0;
        foreach (var ownerNetId in owners)
        {
            for (int i = 0; i < coinsPerPlayer; i++)
            {
                if (slotIdx >= grid.SlotCount) break;

                var slot = grid.GetSlot(slotIdx++);
                SpawnCoinForOwner(ownerNetId, slot.position, slot.rotation);
            }
        }
    }

    [Server]
    void SpawnCoinForOwner(uint ownerNetId, Vector3 worldPos, Quaternion worldRot)
    {
        var go = Instantiate(coinPrefab, worldPos, worldRot);

        if (go.transform is RectTransform rt)
        {
            rt.localScale = Vector3.one;
            rt.anchoredPosition3D = Vector3.zero;
        }

        var binding = go.GetComponent<CoinPlayerBinding>() ?? go.AddComponent<CoinPlayerBinding>();
        binding.ownerNetId = ownerNetId;

        if (NetworkServer.spawned.TryGetValue(ownerNetId, out var ownerIdentity) &&
            ownerIdentity.connectionToClient != null)
        {
            NetworkServer.Spawn(go, ownerIdentity.connectionToClient);
        }
        else
        {
            NetworkServer.Spawn(go);
        }
    }
}
