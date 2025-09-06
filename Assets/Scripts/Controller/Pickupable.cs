using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class Pickupable : MonoBehaviour
{
    [Header("Pickup Behaviour")]
    [Tooltip("When held, we make the Rigidbody kinematic and zero-out gravity/velocities.")]
    public bool makeKinematicWhileHeld = true;

    [Tooltip("Optional: temporarily raise sorting order so it renders on top while held.")]
    public bool raiseSortingWhileHeld = true;
    public int raisedSortingOrder = 1000;

    [Tooltip("Optional: offset from the mouse to keep while holding.")]
    public Vector2 holdOffset = Vector2.zero;

    [Tooltip("Follow smoothing toward the cursor (0 = snap, 1 = very smooth).")]
    [Range(0f, 1f)] public float followSmoothing = 0.15f;

    [Header("Safety")]
    [Tooltip("Max distance (world units) we’ll allow from the interactor to pick this up. Set <= 0 to ignore.")]
    public float maxPickupDistance = 0f;

    [HideInInspector] public bool IsHeld { get; private set; }

    Rigidbody2D _rb;
    Collider2D _col;
    SpriteRenderer _sr;
    int _origSortingOrder;
    RigidbodyType2D _origBodyType;
    float _origGravity;
    Vector3 _velocitySmth; // for SmoothDamp (Vector3 version)

    void Awake()
    {
        TryGetComponent(out _rb);
        TryGetComponent(out _col);
        TryGetComponent(out _sr);
        if (_sr) _origSortingOrder = _sr.sortingOrder;
        if (_rb)
        {
            _origBodyType = _rb.bodyType;
            _origGravity = _rb.gravityScale;
        }
    }

    public bool CanBePickedBy(Transform interactor)
    {
        if (maxPickupDistance > 0f)
        {
            float d = Vector2.Distance(interactor.position, transform.position);
            if (d > maxPickupDistance) return false;
        }
        return true;
    }

    public void OnPickup()
    {
        if (IsHeld) return;
        IsHeld = true;

        if (_sr && raiseSortingWhileHeld)
            _sr.sortingOrder = raisedSortingOrder;

        if (_rb && makeKinematicWhileHeld)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    public void OnDrop()
    {
        if (!IsHeld) return;
        IsHeld = false;

        if (_sr && raiseSortingWhileHeld)
            _sr.sortingOrder = _origSortingOrder;

        if (_rb && makeKinematicWhileHeld)
        {
            _rb.bodyType = _origBodyType;
            _rb.gravityScale = _origGravity;
        }
    }

    /// <summary>
    /// Move toward target world position (called by the interactor while held).
    /// </summary>
    public void FollowTo(Vector3 targetWorld, float dt)
    {
        if (!IsHeld) return;

        // Keep original Z (helpful for 2D)
        targetWorld.z = transform.position.z;

        // Offset
        targetWorld += (Vector3)holdOffset;

        if (_rb && makeKinematicWhileHeld)
        {
            // SmoothDamp to avoid jitter — works fine with kinematic RB.
            var current = transform.position;
            var next = Vector3.SmoothDamp(current, targetWorld, ref _velocitySmth, followSmoothing, Mathf.Infinity, dt);
            _rb.MovePosition(next);
        }
        else
        {
            // Transform-based move (no RB or not kinematic)
            transform.position = Vector3.SmoothDamp(transform.position, targetWorld, ref _velocitySmth, followSmoothing, Mathf.Infinity, dt);
        }
    }
}
