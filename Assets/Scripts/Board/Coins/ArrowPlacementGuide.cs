using UnityEngine;

[DisallowMultipleComponent]
public class ArrowPlacementGuide : MonoBehaviour
{
    [Header("Arrow Prefab")]
    public GameObject arrowPrefab;

    [Header("Follow")]
    public Vector2 arrowOffset = new Vector2(0f, -0.35f);

    public bool pointArrowUpTowardCoin = false;

    [Header("Probe Tip")]
    public Transform probeTip;

    GameObject _arrowInstance;
    Transform _arrowRoot;

    CoinDragHandler _drag;

    void Awake()
    {
        _drag = GetComponent<CoinDragHandler>();
        if (_drag)
        {
            _drag.onPickUp.AddListener(OnPickUp);
            _drag.onDrop.AddListener(OnDrop);
        }
    }

    void OnDestroy()
    {
        if (_drag)
        {
            _drag.onPickUp.RemoveListener(OnPickUp);
            _drag.onDrop.RemoveListener(OnDrop);
        }
    }

    void LateUpdate()
    {
        if (_arrowInstance == null) return;

        var coinPos = transform.position;
        var pos = new Vector3(coinPos.x + arrowOffset.x, coinPos.y + arrowOffset.y, coinPos.z);
        _arrowRoot.position = pos;

        if (pointArrowUpTowardCoin)
        {
            Vector2 dir = (Vector2)(coinPos - _arrowRoot.position);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _arrowRoot.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void OnPickUp()
    {
        if (_arrowInstance != null) return;
        if (!arrowPrefab) return;

        _arrowInstance = Instantiate(arrowPrefab);
        _arrowInstance.name = "[PlacementArrow]";
        _arrowRoot = _arrowInstance.transform;

        if (!probeTip)
        {
            var tip = new GameObject("ProbeTip");
            tip.transform.SetParent(_arrowRoot, false);
            tip.transform.localPosition = Vector3.zero;
            probeTip = tip.transform;
        }

        LateUpdate();
        _arrowInstance.SetActive(true);
    }

    void OnDrop()
    {
        if (_arrowInstance)
        {
            Destroy(_arrowInstance);
            _arrowInstance = null;
            _arrowRoot = null;
        }
    }

    public Vector2 GetProbeWorld()
    {
        if (probeTip) return probeTip.position;
        var p = transform.position;
        return new Vector2(p.x + arrowOffset.x, p.y + arrowOffset.y);
    }
}
