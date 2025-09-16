using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinClientSanity : NetworkBehaviour
{
    [Tooltip("Set true to print info whenever player clicks/taps on this coin.")]
    public bool logOnClick = true;

    Collider2D _col;
    NetworkCoin _netCoin;
    CoinDragHandler _drag;
    Camera _cam;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        _netCoin = GetComponent<NetworkCoin>();
        _drag = GetComponent<CoinDragHandler>();
    }

    void Start()
    {
        _cam = _drag && _drag.worldCamera ? _drag.worldCamera : Camera.main;
    }

    void Update()
    {
        if (!logOnClick) return;
        if (Input.GetMouseButtonDown(0))
        {
            RunSanityCheck(Input.mousePosition);
        }
        if (Input.touchSupported && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                RunSanityCheck(t.position);
        }
    }

    void RunSanityCheck(Vector3 screenPos)
    {
        if (!_cam) _cam = Camera.main;

        // Ray-plane intersection at coin Z
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
        Ray ray = _cam ? _cam.ScreenPointToRay(screenPos) : new Ray(new Vector3(screenPos.x, screenPos.y, -10f), Vector3.forward);
        Vector3 worldPoint = transform.position;
        if (plane.Raycast(ray, out float enter))
            worldPoint = ray.GetPoint(enter);

        bool overCollider = _col && _col.OverlapPoint(worldPoint);

        uint localPlayerId = NetworkClient.localPlayer ? NetworkClient.localPlayer.netId : 0;
        uint ownerId = _netCoin ? _netCoin.ownerNetId : 0;

        Debug.Log(
            $"[ClientSanity] coin={name} " +
            $"localPlayerId={localPlayerId} ownerId={ownerId} isLocalOwner={_netCoin?.IsLocalOwner()} " +
            $"camera={(_cam ? _cam.name : "<null>")} worldPoint={worldPoint} overCollider={overCollider} " +
            $"colliderEnabled={_col?.enabled} dragZ={_drag?.dragZ}");
    }
}
