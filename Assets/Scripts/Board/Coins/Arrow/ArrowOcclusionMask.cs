using System.Collections.Generic;
using UnityEngine;

public class ArrowOcclusionMask : MonoBehaviour
{
    public enum FeatherMode { None, SoftSpriteEdge, ShaderFeather }

    [Header("Probe hookup")]
    public float zOffset = 0f;
    public Vector3 worldNudge = Vector3.zero;

    [Header("Capsule sizing")]
    [Min(0f)] public float baseRadius = 0.45f;
    [Min(0f)] public float lengthPadding = 0.25f;

    [Header("Independent stretch")]
    [Min(0.01f)] public float stretchX = 1f;
    [Min(0.01f)] public float stretchY = 1f;

    [Header("SpriteMask")]
    public Sprite capsuleSprite;
    public bool spriteIsVertical = true;
    [Min(0.02f)] public float reapplyInterval = 0.25f;

    [Header("Feathering")]
    public FeatherMode feather = FeatherMode.SoftSpriteEdge;
    [Min(0.001f)] public float featherWidth = 0.2f;

    [Header("Who to affect")]
    public LayerMask coinRootLayers = ~0;
    public string coinRootTag = "";

    [Header("ShaderFeather Material")]
    public Material softFeatherMat;

    Transform _maskTf;
    SpriteMask _mask;
    float _scanClock;
    CoinPlacementProbe _activeProbe;

    readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> _prevMask = new();
    readonly Dictionary<SpriteRenderer, Material> _prevMat = new();

    static readonly int _CapsuleP0 = Shader.PropertyToID("_CapsuleP0");
    static readonly int _CapsuleP1 = Shader.PropertyToID("_CapsuleP1");
    static readonly int _CapsuleRadius = Shader.PropertyToID("_CapsuleRadius");
    static readonly int _CapsuleFeather = Shader.PropertyToID("_CapsuleFeather");

    void Awake()
    {
        var go = new GameObject("LocalArrowCapsuleMask");
        go.hideFlags = HideFlags.DontSave;
        _maskTf = go.transform;
        _mask = go.AddComponent<SpriteMask>();
        _mask.sprite = capsuleSprite;
        _mask.isCustomRangeActive = true;
        _mask.frontSortingLayerID = 0;
        _mask.backSortingLayerID = 0;
        _mask.frontSortingOrder = 32767;
        _mask.backSortingOrder = -32768;
        _mask.enabled = false;
        _maskTf.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        ClearAllOverrides();
        if (_maskTf) Destroy(_maskTf.gameObject);
    }

    void Update()
    {
        var probe = CoinPlacementProbe.Active;
        if (!probe || !probe.gameObject.activeInHierarchy)
        {
            DeactivateAll();
            return;
        }

        if (probe.requireInsideGridToShow && probe.gridMask)
        {
            var cam = probe.uiCamera ? probe.uiCamera : Camera.main;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(
                probe.gridMask, probe.GetProbeScreenPosition(), cam);
            if (!inside) { DeactivateAll(); return; }
        }

        _activeProbe = probe;
        UpdateCapsuleTransformAndApply(probe);

        _scanClock += Time.deltaTime;
        if (_scanClock >= reapplyInterval)
        {
            _scanClock = 0f;
            ApplyOverrides();
        }
    }

    void DeactivateAll()
    {
        if (_maskTf && _maskTf.gameObject.activeSelf) _maskTf.gameObject.SetActive(false);
        if (_mask && _mask.enabled) _mask.enabled = false;

        Shader.SetGlobalVector(_CapsuleP0, Vector4.zero);
        Shader.SetGlobalVector(_CapsuleP1, Vector4.zero);
        Shader.SetGlobalFloat(_CapsuleRadius, 0f);
        Shader.SetGlobalFloat(_CapsuleFeather, 0f);

        _activeProbe = null;
        _scanClock = 0f;
        ClearAllOverrides();
    }

    void UpdateCapsuleTransformAndApply(CoinPlacementProbe probe)
    {
        var coinPos = probe.transform.position;
        var tipPos = probe.GetProbeWorld();
        var delta = tipPos - coinPos;
        float dist = delta.magnitude;
        if (dist < 1e-4f) dist = 1e-4f;

        var center = coinPos + 0.5f * delta + worldNudge;
        _maskTf.position = new Vector3(center.x, center.y, probe.transform.position.z + zOffset);

        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (spriteIsVertical) angleDeg -= 90f;
        _maskTf.rotation = Quaternion.Euler(0f, 0f, angleDeg);

        float diameter = Mathf.Max(0.0001f, 2f * baseRadius) * stretchX;
        float length = Mathf.Max(diameter, (dist + lengthPadding * 2f) * stretchY);

        var sprite = _mask.sprite ? _mask.sprite : capsuleSprite;
        if (_mask.sprite != sprite) _mask.sprite = sprite;
        if (sprite)
        {
            Vector2 sprSize = sprite.bounds.size;
            float sx = diameter / Mathf.Max(1e-4f, sprSize.x);
            float sy = length / Mathf.Max(1e-4f, sprSize.y);
            _maskTf.localScale = new Vector3(sx, sy, 1f);
        }

        var coinSR = probe.GetComponent<SpriteRenderer>();
        if (coinSR)
        {
            _mask.frontSortingLayerID = coinSR.sortingLayerID;
            _mask.backSortingLayerID = coinSR.sortingLayerID;
        }

        if (feather == FeatherMode.ShaderFeather)
        {
            if (_maskTf.gameObject.activeSelf) _maskTf.gameObject.SetActive(false);
            if (_mask.enabled) _mask.enabled = false;

            Shader.SetGlobalVector(_CapsuleP0, coinPos);
            Shader.SetGlobalVector(_CapsuleP1, tipPos);
            Shader.SetGlobalFloat(_CapsuleRadius, baseRadius);
            Shader.SetGlobalFloat(_CapsuleFeather, featherWidth);
        }
        else
        {
            if (!_maskTf.gameObject.activeSelf) _maskTf.gameObject.SetActive(true);
            if (!_mask.enabled) _mask.enabled = true;

            Shader.SetGlobalFloat(_CapsuleFeather, 0f);
        }

        ApplyOverrides();
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
                if (sr.sprite == null) continue;

                if (p == _activeProbe)
                {
                    _prevMask[sr] = sr.maskInteraction;
                    sr.maskInteraction = SpriteMaskInteraction.None;
                    continue;
                }

                if (feather == FeatherMode.ShaderFeather && softFeatherMat)
                {
                    if (!_prevMat.ContainsKey(sr)) _prevMat[sr] = sr.sharedMaterial;
                    sr.sharedMaterial = softFeatherMat;

                    if (!_prevMask.ContainsKey(sr)) _prevMask[sr] = sr.maskInteraction;
                    sr.maskInteraction = SpriteMaskInteraction.None;
                }
                else
                {
                    if (!_prevMask.ContainsKey(sr)) _prevMask[sr] = sr.maskInteraction;
                    sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                }
            }
        }
    }

    void ClearAllOverrides()
    {
        if (_prevMask.Count > 0)
        {
            foreach (var kv in _prevMask)
                if (kv.Key) kv.Key.maskInteraction = kv.Value;
            _prevMask.Clear();
        }

        if (_prevMat.Count > 0)
        {
            foreach (var kv in _prevMat)
                if (kv.Key) kv.Key.sharedMaterial = kv.Value;
            _prevMat.Clear();
        }
    }
}
