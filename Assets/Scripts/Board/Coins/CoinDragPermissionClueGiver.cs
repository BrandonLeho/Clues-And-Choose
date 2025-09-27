using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkCoin))]
public class CoinDragPermissionClueGiver : MonoBehaviour, ICoinDragPermission
{
    NetworkCoin _coin;

    void Awake()
    {
        _coin = GetComponent<NetworkCoin>();
    }

    public bool CanBeginDrag()
    {
        if (ClueGiverState.IsLocalPlayerClueGiver())
            return true; //originally false

        return true;
    }
}
