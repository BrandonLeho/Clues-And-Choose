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

    [Header("Stretch")]
    public float stretchX = 1f;
    public float stretchY = 1f;

    [Header("Feather")]
    public bool feather = true;
    public float featherWidth = 0.1f;
    public Color featherColor = new Color(0f, 0f, 0f, 0.25f);
    public int featherOrderOffset = 1;

    [Header("Affect Which Renderers")]
    public LayerMask coinRootLayers = ~0;
    public string coinRootTag = "";

    [Header("Refresh")]
    [Min(0.02f)] public float reapplyInterval = 0.25f;

    SpriteMask m;
    Transform mt;
    SpriteRenderer featherSR;
    CoinPlacementProbe active;
    float clk;
    readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> prev = new();

    void Awake()
    {
        var go = new GameObject("LocalArrowCapsuleMask");
        go.hideFlags = HideFlags.DontSave;
        mt = go.transform;
        m = go.AddComponent<SpriteMask>();
        m.sprite = capsuleSprite;
        m.isCustomRangeActive = true;
        m.frontSortingLayerID = 0;
        m.backSortingLayerID = 0;
        m.frontSortingOrder = 32767;
        m.backSortingOrder = -32768;
        m.enabled = false;
        mt.gameObject.SetActive(false);

        var fgo = new GameObject("Feather");
        fgo.hideFlags = HideFlags.DontSave;
        fgo.transform.SetParent(mt, false);
        featherSR = fgo.AddComponent<SpriteRenderer>();
        featherSR.sprite = capsuleSprite;
        featherSR.maskInteraction = SpriteMaskInteraction.None;
        featherSR.enabled = false;
    }

    void OnDestroy()
    {
        ClearAllOverrides();
        if (mt) Destroy(mt.gameObject);
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

        active = probe;
        ActivateMaskFor(probe);

        clk += Time.deltaTime;
        if (clk >= reapplyInterval)
        {
            clk = 0f;
            ApplyOverrides();
        }
    }

    void ActivateMaskFor(CoinPlacementProbe probe)
    {
        var coinPos = probe.transform.position;
        var tipPos = probe.GetProbeWorld();
        var d = tipPos - coinPos;
        var dist = d.magnitude;
        if (dist < 1e-4f) dist = 1e-4f;

        var center = coinPos + 0.5f * d + worldNudge;
        mt.position = new Vector3(center.x, center.y, probe.transform.position.z + zOffset);

        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        if (spriteIsVertical) ang -= 90f;
        mt.rotation = Quaternion.Euler(0f, 0f, ang);

        var sprite = m.sprite ? m.sprite : capsuleSprite;
        if (!sprite) return;
        if (!m.sprite) m.sprite = sprite;

        Vector2 size = sprite.bounds.size;
        float desiredDiameter = Mathf.Max(0.0001f, 2f * radius) * Mathf.Max(0.0001f, stretchX);
        float desiredLength = Mathf.Max(desiredDiameter, dist + lengthPadding * 2f) * Mathf.Max(0.0001f, stretchY);
        float sx = desiredDiameter / Mathf.Max(1e-4f, size.x);
        float sy = desiredLength / Mathf.Max(1e-4f, size.y);
        mt.localScale = new Vector3(sx, sy, 1f);

        var coinSR = probe.GetComponent<SpriteRenderer>();
        if (coinSR)
        {
            m.frontSortingLayerID = coinSR.sortingLayerID;
            m.backSortingLayerID = coinSR.sortingLayerID;
            featherSR.sortingLayerID = coinSR.sortingLayerID;
            featherSR.sortingOrder = coinSR.sortingOrder + featherOrderOffset;
        }

        if (!m.enabled) m.enabled = true;
        if (!mt.gameObject.activeSelf) mt.gameObject.SetActive(true);

        if (feather)
        {
            featherSR.enabled = true;
            featherSR.sprite = sprite;
            featherSR.transform.localRotation = Quaternion.identity;
            float fOuterX = (desiredDiameter + featherWidth * 2f) / Mathf.Max(1e-4f, size.x);
            float fOuterY = (desiredLength + featherWidth * 2f) / Mathf.Max(1e-4f, size.y);
            featherSR.transform.localScale = new Vector3(fOuterX, fOuterY, 1f);
            featherSR.color = featherColor;
        }
        else
        {
            featherSR.enabled = false;
        }

        ApplyOverrides();
    }

    void DeactivateMask()
    {
        if (mt && mt.gameObject.activeSelf) mt.gameObject.SetActive(false);
        if (m && m.enabled) m.enabled = false;
        active = null;
        clk = 0f;
        ClearAllOverrides();
        if (featherSR) featherSR.enabled = false;
    }

    void ApplyOverrides()
    {
        if (!active) return;
        ClearAllOverrides();

        var allProbes = FindObjectsByType<CoinPlacementProbe>(FindObjectsSortMode.None);
        foreach (var p in allProbes)
        {
            if (!p) continue;
            if (coinRootLayers != (coinRootLayers | (1 << p.gameObject.layer))) continue;
            if (!string.IsNullOrEmpty(coinRootTag) && p.tag != coinRootTag) continue;

            var srs = p.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (!sr) continue;
                var desired = (p == active) ? SpriteMaskInteraction.None
                                            : SpriteMaskInteraction.VisibleOutsideMask;
                if (!prev.ContainsKey(sr)) prev[sr] = sr.maskInteraction;
                sr.maskInteraction = desired;
            }
        }
    }

    void ClearAllOverrides()
    {
        if (prev.Count == 0) return;
        foreach (var kv in prev)
            if (kv.Key) kv.Key.maskInteraction = kv.Value;
        prev.Clear();
    }
}