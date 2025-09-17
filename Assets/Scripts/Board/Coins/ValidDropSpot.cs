using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class ValidDropSpot : MonoBehaviour
{
    [Tooltip("If false, the spot temporarily rejects placement.")]
    public bool enabledForPlacement = true;

    [Tooltip("Use collider bounds center instead of transform.position for centering.")]
    public bool useColliderCenter = true;

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

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = enabledForPlacement ? Color.green : Color.red;
        Vector3 p = useColliderCenter && TryGetComponent<Collider2D>(out var c)
            ? (Vector3)c.bounds.center
            : transform.position;
        Gizmos.DrawWireSphere(p, 0.1f);
    }
#endif
}
