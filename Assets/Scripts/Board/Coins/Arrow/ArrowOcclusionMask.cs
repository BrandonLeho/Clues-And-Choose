using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArrowOcclusionMask : MonoBehaviour
{
    public enum FitMode
    {
        AutoFitCoinToTip,
        ManualXY
    }

    [Header("Mask Visual")]
    public Sprite capsuleSprite;
    public bool spriteIsVertical = true;

    [Header("Auto Fit")]
    [Min(0f)] public float radius = 0.45f;
    [Min(0f)] public float lengthPadding = 0.25f;

    [Header("Manual Stretch)")]
    [Min(0.001f)] public float stretchX = 1f;
    [Min(0.001f)] public float stretchY = 2f;

    [Header("Global Stretch Multipliers")]
    [Min(0.001f)] public float mulX = 1f;
    [Min(0.001f)] public float mulY = 1f;

    [Header("Placement")]
    public float zOffset = 0f;
    public Vector3 worldNudge = Vector3.zero;

    [Header("Who Gets Occluded")]
    public LayerMask coinRootLayers = ~0;
    public string coinRootTag = "";

    [Header("Refresh")]
    [Min(0.02f)] public float reapplyInterval = 0.25f;

    [Header("Feathered Look)")]
    public bool enableFeather = true;
    public Color featherColor = new Color(0f, 0f, 0f, 0.25f);
    [Min(0.5f)] public float featherScale = 1.08f;
    public int featherSortingOrderOffset = 50;

    [Header("Sizing Mode")]
    public FitMode sizing = FitMode.AutoFitCoinToTip;

    SpriteMask _mask;
    Transform _maskTf;

    SpriteRenderer _featherSR;
    Transform _featherTf;

    CoinPlacementProbe _activeProbe;
    float _scanClock;

    readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> _prev = new();

    void Awake()
    {
        var maskGO = new GameObject("LocalArrowCapsuleMask");
        maskGO.hideFlags = HideFlags.DontSave;
        _maskTf = maskGO.transform;
        _mask = maskGO.AddComponent<SpriteMask>();
        _mask.sprite = capsuleSprite;
        _mask.isCustomRangeActive = true;
        _mask.frontSortingLayerID = 0;
        _mask.backSortingLayerID = 0;
        _mask.frontSortingOrder = 32767;
        _mask.backSortingOrder = -32768;
        _mask.enabled = false;
        maskGO.SetActive(false);

        var featherGO = new GameObject("LocalArrowCapsuleFeather");
        featherGO.hideFlags = HideFlags.DontSave;
        _featherTf = featherGO.transform;
        _featherSR = featherGO.AddComponent<SpriteRenderer>();
        _featherSR.sprite = capsuleSprite;
        _featherSR.enabled = false;
        featherGO.SetActive(false);
    }

    void OnDestroy()
    {
        ClearAllOverrides();
        if (_maskTf) Destroy(_maskTf.gameObject);
        if (_featherTf) Destroy(_featherTf.gameObject);
    }

    void Update()
    {
        var probe = CoinPlacementProbe.Active;
        if (!probe || !probe.gameObject.activeInHierarchy)
        {
            DeactivateMask();
            return;
        }

        if (probe.requireInsideGridToShow && probe.gridMask)
        {
            var cam = probe.uiCamera ? probe.uiCamera : Camera.main;
            var inside = RectTransformUtility.RectangleContainsScreenPoint(
                probe.gridMask, probe.GetProbeScreenPosition(), cam);
            if (!inside) { DeactivateMask(); return; }
        }

        _activeProbe = probe;
        ActivateAndSizeFor(probe);

        _scanClock += Time.deltaTime;
        if (_scanClock >= reapplyInterval)
        {
            _scanClock = 0f;
            ApplyOverrides();
        }
    }

    void ActivateAndSizeFor(CoinPlacementProbe probe)
    {
        if (!_mask.sprite && capsuleSprite) _mask.sprite = capsuleSprite;
        if (!_featherSR.sprite && capsuleSprite) _featherSR.sprite = capsuleSprite;
        if (!_mask.sprite) return;

        var coinPos = probe.transform.position;
        var tipPos = probe.GetProbeWorld();

        var delta = tipPos - coinPos;
        var dist = Mathf.Max(delta.magnitude, 1e-4f);
        var center = coinPos + 0.5f * delta + worldNudge;

        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (spriteIsVertical) angleDeg -= 90f;

        if (!_mask.enabled) _mask.enabled = true;
        if (!_maskTf.gameObject.activeSelf) _maskTf.gameObject.SetActive(true);

        _maskTf.position = new Vector3(center.x, center.y, probe.transform.position.z + zOffset);
        _maskTf.rotation = Quaternion.Euler(0f, 0f, angleDeg);

        var spriteSize = _mask.sprite.bounds.size;
        float desiredDiameter, desiredLength;

        if (sizing == FitMode.AutoFitCoinToTip)
        {
            desiredDiameter = Mathf.Max(0.0001f, 2f * radius);
            desiredLength = Mathf.Max(desiredDiameter, dist + 2f * lengthPadding);
        }
        else
        {
            desiredDiameter = Mathf.Max(0.0001f, stretchX);
            desiredLength = Mathf.Max(0.0001f, stretchY);
        }

        desiredDiameter *= mulX;
        desiredLength *= mulY;

        float sx = desiredDiameter / Mathf.Max(1e-4f, spriteSize.x);
        float sy = desiredLength / Mathf.Max(1e-4f, spriteSize.y);
        _maskTf.localScale = new Vector3(sx, sy, 1f);

        var coinSR = probe.GetComponent<SpriteRenderer>();
        if (coinSR)
        {
            _mask.frontSortingLayerID = coinSR.sortingLayerID;
            _mask.backSortingLayerID = coinSR.sortingLayerID;

            _featherSR.sortingLayerID = coinSR.sortingLayerID;
            _featherSR.sortingOrder = coinSR.sortingOrder + featherSortingOrderOffset;
        }

        if (enableFeather)
        {
            if (!_featherSR.enabled) _featherSR.enabled = true;
            if (!_featherTf.gameObject.activeSelf) _featherTf.gameObject.SetActive(true);

            _featherTf.position = _maskTf.position;
            _featherTf.rotation = _maskTf.rotation;
            _featherTf.localScale = _maskTf.localScale * featherScale;
            _featherSR.color = featherColor;
        }
        else
        {
            if (_featherTf.gameObject.activeSelf) _featherTf.gameObject.SetActive(false);
            if (_featherSR.enabled) _featherSR.enabled = false;
        }

        ApplyOverrides();
    }

    void DeactivateMask()
    {
        if (_maskTf && _maskTf.gameObject.activeSelf) _maskTf.gameObject.SetActive(false);
        if (_mask && _mask.enabled) _mask.enabled = false;

        if (_featherTf && _featherTf.gameObject.activeSelf) _featherTf.gameObject.SetActive(false);
        if (_featherSR && _featherSR.enabled) _featherSR.enabled = false;

        _activeProbe = null;
        _scanClock = 0f;
        ClearAllOverrides();
    }

    void ApplyOverrides()
    {
        if (!_activeProbe) return;

        ClearAllOverrides();

        var allProbes = FindObjectsByType<CoinPlacementProbe>(FindObjectsSortMode.None);
        foreach (var p in allProbes)
        {
            if (!p) continue;
            if (coinRootLayers != (coinRootLayers | (1 << p.gameObject.layer))) continue;
            if (!string.IsNullOrEmpty(coinRootTag) && p.tag != coinRootTag) continue;

            var srs = p.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            foreach (var sr in srs)
            {
                if (!sr) continue;

                var desired = (p == _activeProbe) ? SpriteMaskInteraction.None
                                                  : SpriteMaskInteraction.VisibleOutsideMask;

                if (!_prev.ContainsKey(sr))
                    _prev[sr] = sr.maskInteraction;

                sr.maskInteraction = desired;
            }
        }
    }

    void ClearAllOverrides()
    {
        if (_prev.Count == 0) return;
        foreach (var kv in _prev)
        {
            if (kv.Key) kv.Key.maskInteraction = kv.Value;
        }
        _prev.Clear();
    }
}
