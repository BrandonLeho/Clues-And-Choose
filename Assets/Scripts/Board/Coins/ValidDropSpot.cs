using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class ValidDropSpot : MonoBehaviour
{
    [Header("Index (unique across board)")]
    public int spotIndex = -1;

    [Header("Placement Gate")]
    public bool enabledForPlacement = true;
    public bool useColliderCenter = true;

    [Header("Occupancy (local mirror of server state)")]
    public bool isOccupied = false;
    public GameObject occupant;

    Collider2D _col;

    void Awake()
    {
        if (!_col) _col = GetComponent<Collider2D>();
        if (!_col)
        {
            Debug.LogError($"[SPOT {spotIndex}] Missing Collider2D on {name}");
        }
    }

    /// <summary>
    /// Returns true if the point is inside this spot's collider AND placement is enabled.
    /// Includes debug logging without changing logic.
    /// </summary>
    public bool ContainsPoint(Vector2 worldPoint)
    {
        if (!_col) _col = GetComponent<Collider2D>();
        if (!_col) { Debug.LogWarning($"[SPOT {spotIndex}] No Collider2D on {name}"); return false; }

        if (!enabledForPlacement)
        {
            Debug.Log($"[SPOT {spotIndex}] ContainsPoint early-exit: enabled={enabledForPlacement}");
            return false;
        }

        bool hit = _col.OverlapPoint(worldPoint);
        if (!hit) Debug.Log($"[SPOT {spotIndex}] OverlapPoint miss at {worldPoint}");
        return hit;
    }

    /// <summary>
    /// The world-space position we snap coins to (collider center or transform).
    /// </summary>
    public Vector3 GetCenterWorld()
    {
        if (useColliderCenter && _col)
        {
            var c = _col.bounds.center;
            return new Vector3(c.x, c.y, transform.position.z);
        }
        return transform.position;
    }

    // ====== Server-driven state applications (no logic change, just helpers) ======

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
