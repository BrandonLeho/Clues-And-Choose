using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArrowOcclusionMask : MonoBehaviour
{
    [Header("Mask Visual")]
    public Sprite capsuleSprite;
    [Min(0f)] public float radius = 0.45f;
    [Min(0f)] public float lengthPadding = 0.25f;
    public bool spriteIsVertical = true;

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
    CoinPlacementProbe _activeProbe;
    float _scanClock;

    readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> _prev = new();

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
        ActivateMaskFor(probe);

        _scanClock += Time.deltaTime;
        if (_scanClock >= reapplyInterval)
        {
            _scanClock = 0f;
            ApplyOverrides();
        }
    }

    void ActivateMaskFor(CoinPlacementProbe probe)
    {
        var coinPos = probe.transform.position;
        var tipPos = probe.GetProbeWorld();

        var delta = tipPos - coinPos;
        var dist = delta.magnitude;
        if (dist < 1e-4f) dist = 1e-4f;

        var center = coinPos + 0.5f * delta + worldNudge;
        _maskTf.position = new Vector3(center.x, center.y, probe.transform.position.z + zOffset);

        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (spriteIsVertical) angleDeg -= 90f;
        _maskTf.rotation = Quaternion.Euler(0f, 0f, angleDeg);

        var sprite = _mask.sprite;
        if (!sprite)
        {
            _mask.sprite = capsuleSprite;
            sprite = _mask.sprite;
            if (!sprite) return;
        }
        Vector2 spriteSize = sprite.bounds.size;
        float desiredDiameter = Mathf.Max(0.0001f, 2f * radius);
        float desiredLength = Mathf.Max(desiredDiameter, dist + lengthPadding * 2f);

        float sx = desiredDiameter / Mathf.Max(1e-4f, spriteSize.x);
        float sy = desiredLength / Mathf.Max(1e-4f, spriteSize.y);
        _maskTf.localScale = new Vector3(sx, sy, 1f);

        var coinSR = probe.GetComponent<SpriteRenderer>();
        if (coinSR)
        {
            _mask.frontSortingLayerID = coinSR.sortingLayerID;
            _mask.backSortingLayerID = coinSR.sortingLayerID;
        }

        if (!_mask.enabled) _mask.enabled = true;
        if (!_maskTf.gameObject.activeSelf) _maskTf.gameObject.SetActive(true);

        ApplyOverrides();
    }

    void DeactivateMask()
    {
        if (_maskTf && _maskTf.gameObject.activeSelf)
            _maskTf.gameObject.SetActive(false);
        if (_mask && _mask.enabled)
            _mask.enabled = false;

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
