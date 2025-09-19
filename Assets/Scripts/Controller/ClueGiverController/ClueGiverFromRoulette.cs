using Mirror;
using UnityEngine;

[RequireComponent(typeof(RouletteText))]
public class ClueGiverFromRoulette : NetworkBehaviour
{
    [SerializeField] RouletteText roulette;
    [SerializeField] ClueGiverManager manager;

    bool bound = false;

    void Reset()
    {
        roulette = GetComponent<RouletteText>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        TryBind();
    }

    void OnEnable()
    {
        TryBind();
    }

    void OnDisable()
    {
        if (roulette != null)
            roulette.OnSpinComplete.RemoveListener(OnRouletteWinner);
        bound = false;
    }

    void TryBind()
    {
        if (bound || roulette == null) return;
        if (!isServer) return;

        roulette.OnSpinComplete.RemoveListener(OnRouletteWinner);
        roulette.OnSpinComplete.AddListener(OnRouletteWinner);
        bound = true;
    }

    void OnRouletteWinner(string winnerName, int _)
    {
        if (!isServer || manager == null) return;
        manager.ServerSetInitialByWinner(winnerName);
        roulette.OnSpinComplete.RemoveListener(OnRouletteWinner);
        bound = false;
    }
}
