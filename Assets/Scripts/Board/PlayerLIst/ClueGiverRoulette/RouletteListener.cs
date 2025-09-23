using UnityEngine;

public class RouletteListener : MonoBehaviour
{
    public void OnRouletteWinner(string winnerName, int index)
    {


        if (GameLoopManager.Exists)
        {
            Debug.Log($"Roulette winner picked: {winnerName} (index {index})");
            GameLoopManager.Instance.CmdSetFirstClueGiverByName(winnerName);
        }
    }
}
