using UnityEngine;

public sealed class ChoiceSelectToUnlockAll : MonoBehaviour
{
    public void OnChoiceSelected(CardChoiceSelectController.ChoicePayload payload)
    {
        var notifiers = FindObjectsByType<PlayerChoiceNotifier>(FindObjectsSortMode.None);
        PlayerChoiceNotifier local = null;
        for (int i = 0; i < notifiers.Length; i++)
        {
            if (notifiers[i] && notifiers[i].isLocalPlayer)
            {
                local = notifiers[i];
                break;
            }
        }

        if (local != null)
        {
            local.NotifyChoiceSelected();
        }
        else
        {
            var mgr = FindFirstObjectByType<CoinRoundLockManager>();
            if (mgr) mgr.UnlockAllCoins();
        }
    }
}
