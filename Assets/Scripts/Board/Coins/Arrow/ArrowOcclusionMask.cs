using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArrowOcclusionMask : MonoBehaviour
{
    public enum OcclusionMode { FeatheredFade, HardSpriteMask }

    [Header("Mode")]
    public OcclusionMode mode = OcclusionMode.FeatheredFade;

    [Header("Capsule Shape")]
    [Min(0f)] public float radius = 0.45f;
    public float widthScale = 1.00f;
    public float lengthScale = 1.00f;
    public float extraLengthPadding = 0.00f;

    [Header("Feather (fade)")]
    [Min(0f)] public float feather = 0.20f;
    public bool spriteIsVertical = true;

    [Header("FeatheredFade Material")]
    public Material featheredOccluderMaterial;

    [Header("Optional Placement")]
    public float zOffset = 0f;
    public Vector3 worldNudge = Vector3.zero;

    [Header("Which things to affect")]
    public LayerMask coinRootLayers = ~0;
    public string coinRootTag = "";

    [Header("Refresh")]
    [Min(0.02f)] public float reapplyInterval = 0.25f;

    public Sprite capsuleSprite;
    SpriteMask _mask;
    Transform _maskTf;

    CoinPlacementProbe _activeProbe;
    float _scanClock;

    readonly Dictionary<SpriteRenderer, Material> _prevMat = new();
    readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> _prevMask = new();

    void Awake()
    {
        if (mode == OcclusionMode.HardSpriteMask)
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

        if (mode == OcclusionMode.FeatheredFade)
            ActivateFeathered();
        else
            ActivateHardMask();

        _scanClock += Time.deltaTime;
        if (_scanClock >= reapplyInterval)
        {
            _scanClock = 0f;
            ApplyOverrides();
        }
    }

    void ActivateFeathered()
    {
        if (!featheredOccluderMaterial) return;

        Vector3 baseA = _activeProbe.transform.position;
        Vector3 baseB = _activeProbe.GetProbeWorld();
        Vector2 A = new Vector2(baseA.x, baseA.y);
        Vector2 B = new Vector2(baseB.x, baseB.y);

        Vector2 AB = (B - A);
        float dist = Mathf.Max(1e-4f, AB.magnitude);
        Vector2 dir = AB / dist;

        float extra = extraLengthPadding;
        float add = Mathf.Max(0f, (lengthScale - 1f) * dist * 0.5f);
        Vector2 A2 = A - dir * (add + extra);
        Vector2 B2 = B + dir * (add + extra);
        float r = Mathf.Max(0f, radius * Mathf.Max(0.0001f, widthScale));

        featheredOccluderMaterial.SetVector("_P0", new Vector4(A2.x, A2.y, 0, 0));
        featheredOccluderMaterial.SetVector("_P1", new Vector4(B2.x, B2.y, 0, 0));
        featheredOccluderMaterial.SetFloat("_Radius", r);
        featheredOccluderMaterial.SetFloat("_Feather", Mathf.Max(0f, feather));

        ApplyOverrides();
    }

    void ActivateHardMask()
    {
        if (!_mask) return;

        var coinPos = _activeProbe.transform.position;
        var tipPos = _activeProbe.GetProbeWorld();

        var delta = tipPos - coinPos;
        var dist = delta.magnitude;
        if (dist < 1e-4f) dist = 1e-4f;

        var dir = delta / dist;
        float add = Mathf.Max(0f, (lengthScale - 1f) * dist * 0.5f) + Mathf.Max(0f, extraLengthPadding);
        var A = coinPos - (Vector3)(dir * add);
        var B = tipPos + (Vector3)(dir * add);

        var center = (A + B) * 0.5f + worldNudge;
        _maskTf.position = new Vector3(center.x, center.y, (_activeProbe.transform.position.z + zOffset));

        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (spriteIsVertical) angleDeg -= 90f;
        _maskTf.rotation = Quaternion.Euler(0f, 0f, angleDeg);

        var sprite = _mask.sprite ? _mask.sprite : capsuleSprite;
        if (!sprite) return;
        _mask.sprite = sprite;

        Vector2 spriteSize = sprite.bounds.size;

        float desiredDiameter = Mathf.Max(0.0001f, 2f * radius * Mathf.Max(0.0001f, widthScale));
        float desiredLength = Mathf.Max(desiredDiameter, (B - A).magnitude);

        float sx = desiredDiameter / Mathf.Max(1e-4f, spriteSize.x);
        float sy = desiredLength / Mathf.Max(1e-4f, spriteSize.y);
        _maskTf.localScale = new Vector3(sx, sy, 1f);

        var coinSR = _activeProbe.GetComponent<SpriteRenderer>();
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
        if (_maskTf) _maskTf.gameObject.SetActive(false);
        if (_mask) _mask.enabled = false;

        _activeProbe = null;
        _scanClock = 0f;
        ClearAllOverrides();
    }

    void ApplyOverrides()
    {
        if (!_activeProbe) return;

        ClearAllOverrides();

        var allProbes = FindObjectsOfType<CoinPlacementProbe>(includeInactive: true);
        foreach (var p in allProbes)
        {
            if (!p) continue;
            if (coinRootLayers != (coinRootLayers | (1 << p.gameObject.layer))) continue;
            if (!string.IsNullOrEmpty(coinRootTag) && p.tag != coinRootTag) continue;

            var srs = p.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            foreach (var sr in srs)
            {
                if (!sr) continue;

                if (mode == OcclusionMode.FeatheredFade)
                {
                    if (p == _activeProbe) continue;
                    if (featheredOccluderMaterial)
                    {
                        if (!_prevMat.ContainsKey(sr))
                            _prevMat[sr] = sr.sharedMaterial;

                        sr.sharedMaterial = featheredOccluderMaterial;
                    }
                }
                else
                {
                    var desired = (p == _activeProbe) ? SpriteMaskInteraction.None
                                                      : SpriteMaskInteraction.VisibleOutsideMask;

                    if (!_prevMask.ContainsKey(sr))
                        _prevMask[sr] = sr.maskInteraction;

                    sr.maskInteraction = desired;
                }
            }
        }
    }

    void ClearAllOverrides()
    {
        if (_prevMat.Count > 0)
        {
            foreach (var kv in _prevMat)
                if (kv.Key) kv.Key.sharedMaterial = kv.Value;
            _prevMat.Clear();
        }
        if (_prevMask.Count > 0)
        {
            foreach (var kv in _prevMask)
                if (kv.Key) kv.Key.maskInteraction = kv.Value;
            _prevMask.Clear();
        }
    }
}
