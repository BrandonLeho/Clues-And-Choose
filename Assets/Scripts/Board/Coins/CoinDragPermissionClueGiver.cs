using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkCoin))]
public class CoinDragPermissionClueGiver : MonoBehaviour, ICoinDragPermission
{
    NetworkCoin _coin;
    CoinRejectionFeedback _reject;

    void Awake()
    {
        _coin = GetComponent<NetworkCoin>();
        _reject = GetComponent<CoinRejectionFeedback>();
        if (!_reject) _reject = gameObject.AddComponent<CoinRejectionFeedback>();
    }

    public bool CanBeginDrag()
    {
        if (ClueGiverState.IsLocalPlayerClueGiver() && _coin != null && _coin.IsLocalOwner())
        {
            _reject?.Play();
            return false;
        }
        return true;
    }
}
