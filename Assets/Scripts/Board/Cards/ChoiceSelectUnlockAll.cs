using UnityEngine;

public sealed class ChoiceSelectToUnlockAll : MonoBehaviour
{
    public void OnChoiceSelected(CardChoiceSelectController.ChoicePayload payload)
    {
        var mgr = FindFirstObjectByType<CoinRoundLockManager>();
        if (mgr) mgr.UnlockAllCoins();
    }
}
