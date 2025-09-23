using UnityEngine;

public class RouletteListener : MonoBehaviour
{
    public void OnRouletteWinner(string winnerName, int index)
    {
        Debug.Log($"Roulette winner picked: {winnerName} (index {index})");

        if (GameLoopManager.Exists)
            GameLoopManager.Instance.CmdSetFirstClueGiverByName(winnerName);
    }
}
