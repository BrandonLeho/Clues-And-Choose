using UnityEngine;

public class CoinTurnPlacementGuard : MonoBehaviour, ICoinDragPermission
{
    [SerializeField] bool debugLogs = true;

    public bool CanBeginDrag()
    {
        bool can = CoinPlacementTurnManager.IsLocalPlayersTurn();
        if (debugLogs && !can)
            Debug.LogWarning("[Turn] BeginDrag denied: not your turn");
        return can;
    }
}
