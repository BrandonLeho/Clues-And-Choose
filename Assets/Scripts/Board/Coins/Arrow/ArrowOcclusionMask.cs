using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArrowOcclusionMask : MonoBehaviour
{
    [Header("Mask")]
    public Sprite capsuleSprite;
    [Min(0f)] public float radius = 0.45f;
    [Min(0f)] public float lengthPadding = 0.25f;
    public bool spriteIsVertical = true;
    public float xStretch = 1.0f;
    public float yStretch = 1.0f;

    [Header("Feather Band")]
    public Sprite featherSprite;
    public Material featherMat;
    [Min(0f)] public float featherWidth = 0.2f;
    [Range(0f, 1f)] public float featherAlpha = 0.25f;
    public Color featherColor = new Color(1f, 1f, 1f, 1f);

    [Header("Placement")]
    public float zOffset = 0f;
    public Vector3 worldNudge = Vector3.zero;

    [Header("Affect Which Renderers")]
    public LayerMask coinRootLayers = ~0;
    public string coinRootTag = "";

    [Header("Refresh")]
    [Min(0.02f)] public float reapplyInterval = 0.25f;

    SpriteMask _mask;
    Transform _maskTf;

    SpriteRenderer _featherSR;
    Transform _featherTf;

    readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> _prev = new();
    float _scanClock;
    CoinPlacementProbe _activeProbe;

    void Awake()
    {
        var maskGo = new GameObject("LocalArrowCapsuleMask");
        maskGo.hideFlags = HideFlags.DontSave;
        _maskTf = maskGo.transform;
        _mask = maskGo.AddComponent<SpriteMask>();
        _mask.sprite = capsuleSprite;
        _mask.isCustomRangeActive = true;
        _mask.frontSortingLayerID = 0;
        _mask.backSortingLayerID = 0;
        _mask.frontSortingOrder = 10000;
        _mask.backSortingOrder = -10000;
        _mask.enabled = false;
        maskGo.SetActive(false);

        var featherGo = new GameObject("LocalArrowCapsuleFeather");
        featherGo.hideFlags = HideFlags.DontSave;
        _featherTf = featherGo.transform;
        _featherSR = featherGo.AddComponent<SpriteRenderer>();
        _featherSR.sprite = featherSprite ? featherSprite : capsuleSprite;
        _featherSR.sharedMaterial = featherMat;
        _featherSR.sortingOrder = 10000;
        featherGo.SetActive(false);
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
            Deactivate();
            return;
        }

        if (probe.requireInsideGridToShow && probe.gridMask)
        {
            var cam = probe.uiCamera ? probe.uiCamera : Camera.main;
            var inside = RectTransformUtility.RectangleContainsScreenPoint(
                probe.gridMask, probe.GetProbeScreenPosition(), cam);
            if (!inside) { Deactivate(); return; }
        }

        _activeProbe = probe;
        ActivateAndPose(probe);

        _scanClock += Time.deltaTime;
        if (_scanClock >= reapplyInterval)
        {
            _scanClock = 0f;
            ApplyOverrides();
        }
    }

    void ActivateAndPose(CoinPlacementProbe probe)
    {
        var coinPos = probe.transform.position;
        var tipPos = probe.GetProbeWorld();
        var delta = tipPos - coinPos;
        var dist = Mathf.Max(delta.magnitude, 1e-4f);

        var center = coinPos + 0.5f * delta + worldNudge;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (spriteIsVertical) angle -= 90f;

        _maskTf.position = new Vector3(center.x, center.y, probe.transform.position.z + zOffset);
        _maskTf.rotation = Quaternion.Euler(0f, 0f, angle);

        float diameter = Mathf.Max(0.0001f, 2f * radius);
        float length = Mathf.Max(diameter, dist + lengthPadding * 2f);

        diameter *= Mathf.Max(1e-4f, xStretch);
        length *= Mathf.Max(1e-4f, yStretch);

        var sprite = _mask.sprite ? _mask.sprite : capsuleSprite;
        if (_mask.sprite != sprite) _mask.sprite = sprite;
        var b = sprite.bounds.size;
        float sx = diameter / Mathf.Max(1e-4f, b.x);
        float sy = length / Mathf.Max(1e-4f, b.y);
        _maskTf.localScale = new Vector3(sx, sy, 1f);

        var coinSR = probe.GetComponent<SpriteRenderer>();
        if (coinSR)
        {
            _mask.frontSortingLayerID = coinSR.sortingLayerID;
            _mask.backSortingLayerID = coinSR.sortingLayerID;

            _featherSR.sortingLayerID = coinSR.sortingLayerID;
        }

        if (!_mask.enabled) _mask.enabled = true;
        if (!_maskTf.gameObject.activeSelf) _maskTf.gameObject.SetActive(true);

        _featherTf.position = _maskTf.position;
        _featherTf.rotation = _maskTf.rotation;
        _featherTf.localScale = _maskTf.localScale;

        if (_featherSR.sharedMaterial)
        {
            var m = _featherSR.sharedMaterial;
            m.SetFloat("_FeatherWidth", Mathf.Max(0.0001f, featherWidth));
            m.SetFloat("_FeatherAlpha", Mathf.Clamp01(featherAlpha));
            m.SetColor("_FeatherColor", featherColor);
            m.SetFloat("_SpriteHeight", b.y * sy);
            m.SetFloat("_SpriteWidth", b.x * sx);
        }

        if (!_featherTf.gameObject.activeSelf) _featherTf.gameObject.SetActive(true);

        ApplyOverrides();
    }

    void Deactivate()
    {
        if (_maskTf) _maskTf.gameObject.SetActive(false);
        if (_mask) _mask.enabled = false;
        if (_featherTf) _featherTf.gameObject.SetActive(false);

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
