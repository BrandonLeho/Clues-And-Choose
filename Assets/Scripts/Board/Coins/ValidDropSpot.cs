using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class ValidDropSpot : MonoBehaviour
{
    [Header("Placement Gate")]
    public bool enabledForPlacement = true;

    [Header("Occupancy (local view)")]
    public bool isOccupied = false;
    public GameObject occupant;

    [Header("Index")]
    public int spotIndex = -1;

    Collider2D _col;

    void Awake() { _col = GetComponent<Collider2D>(); }

    public bool ContainsPoint(Vector2 worldPoint)
        => _col && enabledForPlacement && _col.OverlapPoint(worldPoint);

    public Vector3 GetCenterWorld()
    {
        var c = _col ? _col.bounds.center : transform.position;
        return new Vector3(c.x, c.y, transform.position.z);
    }

    public void SetOccupantLocal(GameObject coin)
    {
        occupant = coin;
        isOccupied = coin != null;
        enabledForPlacement = (coin == null);
    }

    public void Release(GameObject coin)
    {
        var ni = coin ? coin.GetComponent<Mirror.NetworkIdentity>() : null;

        if (BoardSpotsNet.Instance && spotIndex >= 0 && ni)
        {
            BoardSpotsNet.RequestRelease(spotIndex, ni);
            return;
        }

        if (occupant == coin) SetOccupantLocal(null);
    }

}
