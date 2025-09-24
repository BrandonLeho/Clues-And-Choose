using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class ValidDropSpot : MonoBehaviour
{
    public bool enabledForPlacement = true;
    public bool useColliderCenter = true;

    [Header("Occupancy")]
    public bool isOccupied = false;

    public GameObject occupant;

    Collider2D _col;

    void Awake() => _col = GetComponent<Collider2D>();

    public bool ContainsPoint(Vector2 worldPoint)
    {
        if (!_col || !enabledForPlacement) return false;
        return _col.OverlapPoint(worldPoint);
    }

    public Vector3 GetCenterWorld()
    {
        if (useColliderCenter && _col)
        {
            var c = _col.bounds.center;
            return new Vector3(c.x, c.y, transform.position.z);
        }
        return transform.position;
    }

    public bool TryOccupy(GameObject coin)
    {
        if (!enabledForPlacement || isOccupied) return false;
        isOccupied = true;
        occupant = coin;
        enabledForPlacement = false;
        return true;
    }

    public void Release(GameObject coin)
    {
        if (occupant == coin)
        {
            isOccupied = false;
            occupant = null;
            enabledForPlacement = true;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = (!isOccupied && enabledForPlacement) ? Color.green : Color.red;
        Vector3 p = useColliderCenter && TryGetComponent<Collider2D>(out var c)
            ? (Vector3)c.bounds.center
            : transform.position;
        Gizmos.DrawWireSphere(p, 0.1f);
    }
#endif
}