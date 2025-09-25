using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class ValidDropSpot : MonoBehaviour
{
    [Header("Index (unique across board)")]
    public int spotIndex = -1;

    public bool enabledForPlacement = true;
    public bool useColliderCenter = true;

    [Header("Occupancy (local mirror of server state)")]
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

    public void ForceOccupy(GameObject coin)
    {
        isOccupied = true;
        occupant = coin;
        enabledForPlacement = false;
    }

    public void ForceClear()
    {
        isOccupied = false;
        occupant = null;
        enabledForPlacement = true;
    }
}
