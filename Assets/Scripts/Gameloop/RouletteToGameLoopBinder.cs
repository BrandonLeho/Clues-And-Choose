using Mirror;
using UnityEngine;

public class RouletteToGameLoopBinder : MonoBehaviour
{
    [SerializeField] private RouletteText roulette;

    private void OnEnable()
    {
        if (roulette != null)
            roulette.OnSpinComplete.AddListener(OnRouletteComplete);
    }

    private void OnDisable()
    {
        if (roulette != null)
            roulette.OnSpinComplete.RemoveListener(OnRouletteComplete);
    }

    private void OnRouletteComplete(string winnerName, int _)
    {
        if (GameLoopManager.Exists)
            GameLoopManager.Instance.CmdSetFirstClueGiverByName(winnerName);
    }
}
