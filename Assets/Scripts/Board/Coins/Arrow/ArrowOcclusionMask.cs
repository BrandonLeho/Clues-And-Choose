using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CoinPlacementProbe))]
[DisallowMultipleComponent]
public class ArrowOcclusionMask : MonoBehaviour
{
    [Header("Mask Shape")]
    public Sprite capsuleSprite;
    public float extraLength = 0.25f;
    public float radius = 0.6f;
    public float tipPad = 0.15f;

    [Header("What gets masked")]
    public bool onlyCoinsAndArrows = true;
    public bool useHeuristicNameMatch = true;
    public string[] nameHints = new[] { "coin", "arrow" };

    [Header("Sorting Range for Mask")]
    public int orderRangeBelow = 200;
    public int orderRangeAbove = 200;

    [Header("Update")]
    public bool liveUpdate = true;

    CoinPlacementProbe _probe;
    SpriteRenderer _localCoinSR;
    GameObject _maskGO;
    SpriteMask _mask;
    readonly List<SpriteRenderer> _affected = new();
    readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> _originalMI = new();

    void Awake()
    {
        _probe = GetComponent<CoinPlacementProbe>();
        _localCoinSR = GetComponent<SpriteRenderer>();
    }

    void OnDisable() => TearDownMask();

    void Update()
    {
        bool shouldBeActive = CoinPlacementProbe.Active == _probe;
        if (shouldBeActive && _mask == null) SetupMask();
        if (!shouldBeActive && _mask != null) TearDownMask();
        if (shouldBeActive && _mask != null && liveUpdate) PoseMaskToCoinAndProbe();
    }

    void SetupMask()
    {
        if (capsuleSprite == null) return;

        _maskGO = new GameObject("ArrowOcclusionMask", typeof(SpriteMask));
        _maskGO.layer = gameObject.layer;
        _mask = _maskGO.GetComponent<SpriteMask>();
        _mask.sprite = capsuleSprite;

        _maskGO.transform.SetParent(transform, true);

        int layerID = _localCoinSR ? _localCoinSR.sortingLayerID : 0;
        int coinOrder = _localCoinSR ? _localCoinSR.sortingOrder : 0;
        _mask.frontSortingLayerID = layerID;
        _mask.backSortingLayerID = layerID;
        _mask.frontSortingOrder = coinOrder + Mathf.Abs(orderRangeAbove);
        _mask.backSortingOrder = coinOrder - Mathf.Abs(orderRangeBelow);

        CollectTargets();
        ApplyMaskInteraction(SpriteMaskInteraction.VisibleInsideMask);
        PoseMaskToCoinAndProbe();
    }

    void TearDownMask()
    {
        foreach (var kv in _originalMI)
            if (kv.Key) kv.Key.maskInteraction = kv.Value;
        _originalMI.Clear();
        _affected.Clear();

        if (_maskGO) Destroy(_maskGO);
        _maskGO = null;
        _mask = null;
    }

    void CollectTargets()
    {
        _affected.Clear();
        _originalMI.Clear();

        var allSR = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var sr in allSR)
        {
            if (!sr || sr == _localCoinSR) continue;
            if (onlyCoinsAndArrows)
            {
                bool looksLikeCoinOrArrow = sr.GetComponentInParent<CoinDragHandler>() != null;
                if (!looksLikeCoinOrArrow && useHeuristicNameMatch)
                {
                    string n = sr.name.ToLowerInvariant();
                    foreach (var hint in nameHints)
                    {
                        if (!string.IsNullOrEmpty(hint) && n.Contains(hint.ToLowerInvariant()))
                        {
                            looksLikeCoinOrArrow = true;
                            break;
                        }
                    }
                }
                if (!looksLikeCoinOrArrow) continue;
            }
            _affected.Add(sr);
        }

        if (_localCoinSR && !_affected.Contains(_localCoinSR))
            _affected.Add(_localCoinSR);

        var childSR = GetComponentsInChildren<SpriteRenderer>(false);
        foreach (var sr in childSR)
            if (!_affected.Contains(sr)) _affected.Add(sr);
    }

    void ApplyMaskInteraction(SpriteMaskInteraction mode)
    {
        foreach (var sr in _affected)
        {
            if (!sr) continue;
            if (!_originalMI.ContainsKey(sr))
                _originalMI[sr] = sr.maskInteraction;
            sr.maskInteraction = mode;
        }
    }

    void PoseMaskToCoinAndProbe()
    {
        if (_mask == null) return;

        Vector3 a = transform.position;
        Vector3 b = _probe ? _probe.GetProbeWorld() : a;

        Vector3 dir = b - a;
        float dist = dir.magnitude + tipPad;
        if (dist < Mathf.Epsilon) dist = 0.001f;
        Vector3 mid = a + dir.normalized * (dist * 0.5f);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        _maskGO.transform.position = mid;
        _maskGO.transform.rotation = Quaternion.Euler(0f, 0f, ang);

        var bounds = capsuleSprite.bounds.size;
        float spriteW = Mathf.Max(1e-4f, bounds.x);
        float spriteH = Mathf.Max(1e-4f, bounds.y);
        float targetLen = dist + Mathf.Max(0f, extraLength);
        float targetDia = Mathf.Max(0.01f, radius * 2f);

        float sx = targetDia / spriteW;
        float sy = targetLen / spriteH;
        _maskGO.transform.localScale = new Vector3(sx, sy, 1f);
    }
}
