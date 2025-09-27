using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalArrowFocusMask : MonoBehaviour
{
    [Header("What to fade/mask")]
    public LayerMask targetLayers;
    public bool includeInactiveObjects = false;

    [Header("Ellipse")]
    public float radiusX = 1.25f;
    public float radiusY = 1.25f;

    [Header("Feather")]
    public float feather = 0.35f;

    [Header("Fade Settings")]
    [Range(0f, 1f)] public float outsideAlpha = 0f;
    public float refreshListEvery = 0.35f;

    readonly Dictionary<SpriteRenderer, Color> _original = new Dictionary<SpriteRenderer, Color>(256);
    readonly List<SpriteRenderer> _targets = new List<SpriteRenderer>(256);
    float _rescanTimer;
    bool _isActive;

    void OnDisable() { RestoreAll(); _isActive = false; }
    void OnDestroy() { RestoreAll(); _isActive = false; }

    void Update()
    {
        var probe = CoinPlacementProbe.Active;
        bool shouldBeActive = probe != null;

        if (shouldBeActive && !_isActive)
        {
            _isActive = true;
            BuildTargetList();
        }
        else if (!shouldBeActive && _isActive)
        {
            _isActive = false;
            RestoreAll();
        }

        if (!_isActive) return;

        _rescanTimer -= Time.deltaTime;
        if (_rescanTimer <= 0f)
        {
            _rescanTimer = Mathf.Max(0.05f, refreshListEvery);
            BuildTargetList();
        }

        Vector3 center = probe.GetProbeWorld();

        float rx = Mathf.Max(0.0001f, radiusX);
        float ry = Mathf.Max(0.0001f, radiusY);
        float featherBand = Mathf.Max(0.0001f, feather);

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var sr = _targets[i];
            if (!sr)
            {
                _targets.RemoveAt(i);
                continue;
            }

            if (sr.transform && probe && sr.transform.IsChildOf(probe.transform))
            {
                RestoreOne(sr);
                continue;
            }

            Vector3 p = sr.bounds.center;
            float dx = (p.x - center.x) / rx;
            float dy = (p.y - center.y) / ry;
            float n = Mathf.Sqrt(dx * dx + dy * dy);

            float t = 0f;
            if (n > 1f)
                t = Mathf.Clamp01((n - 1f) / (featherBand / Mathf.Min(rx, ry)));

            float a = Mathf.Lerp(1f, outsideAlpha, Smooth01(t));

            if (!_original.TryGetValue(sr, out var baseColor))
            {
                baseColor = sr.color;
                _original[sr] = baseColor;
            }

            var c = baseColor;
            c.a = baseColor.a * a;
            sr.color = c;
        }
    }

    void BuildTargetList()
    {
        RestoreAll();
        _targets.Clear();

#if UNITY_2023_1_OR_NEWER
        var found = Object.FindObjectsByType<SpriteRenderer>(includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var found = Object.FindObjectsOfType<SpriteRenderer>(includeInactiveObjects);
#endif
        foreach (var sr in found)
        {
            if (!sr) continue;
            if (((1 << sr.gameObject.layer) & targetLayers.value) == 0) continue;
            _targets.Add(sr);
        }
    }

    void RestoreAll()
    {
        foreach (var kv in _original)
        {
            if (kv.Key) kv.Key.color = kv.Value;
        }
        _original.Clear();
    }

    void RestoreOne(SpriteRenderer sr)
    {
        if (_original.TryGetValue(sr, out var col))
        {
            if (sr) sr.color = col;
            _original.Remove(sr);
        }
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x * (x * (x * 6f - 15f) + 10f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var probe = CoinPlacementProbe.Active;
        if (!probe) return;
        Vector3 c = probe.GetProbeWorld();
        Color g = new Color(0f, 1f, 0.5f, 0.3f);
        Color f = new Color(0.2f, 0.8f, 1f, 0.15f);
        UnityEditor.Handles.color = g;
        UnityEditor.Handles.DrawWireDisc(c, Vector3.forward, radiusX);
        UnityEditor.Handles.DrawWireDisc(c, Vector3.forward, radiusY);
        UnityEditor.Handles.color = f;
        UnityEditor.Handles.DrawWireDisc(c, Vector3.forward, Mathf.Max(radiusX, radiusY) + feather);
    }
#endif
}
