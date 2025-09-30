using UnityEngine;

public sealed class CoinLockedState : MonoBehaviour, ICoinDragPermission
{
    [SerializeField] bool locked = true;

    [SerializeField] Collider2D hitCollider;

    void Reset()
    {
        hitCollider = GetComponent<Collider2D>();
    }

    void OnEnable()
    {
        ApplyColliderState();
        CoinRoundLockManager.Register(this);
    }

    void OnDisable()
    {
        CoinRoundLockManager.Unregister(this);
    }

    public bool CanBeginDrag() => !locked;

    public void SetLocked(bool v)
    {
        if (locked == v) return;
        locked = v;
        ApplyColliderState();
    }

    public void Unlock() => SetLocked(false);
    public void Lock() => SetLocked(true);

    void ApplyColliderState()
    {
        if (hitCollider) hitCollider.enabled = !locked;
    }
}
