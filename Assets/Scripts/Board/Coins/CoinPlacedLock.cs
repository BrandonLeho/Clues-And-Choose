using UnityEngine;

public class CoinPlacedLock : MonoBehaviour, ICoinDragPermission
{
    public bool locked;

    public bool CanBeginDrag() => !locked;

    public void Lock() => locked = true;
    public void Unlock() => locked = false;
}
