using Mirror;
using UnityEngine;

public sealed class PlacingPhaseController : NetworkBehaviour
{
    public static PlacingPhaseController Instance { get; private set; }

    void Awake() => Instance = this;

    [Command(requiresAuthority = false)]
    public void CmdStartPlacingPhase()
    {
        if (CoinPlacementTurnManager.Instance)
        {
            Debug.Log("[Phase] Start Placing: building order and selecting first placer");
            CoinPlacementTurnManager.Instance.ServerBeginCycleAtFirst();
        }

    }
}
