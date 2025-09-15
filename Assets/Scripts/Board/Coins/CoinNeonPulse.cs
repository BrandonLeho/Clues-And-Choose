using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class CoinPulseController : MonoBehaviour
{
    [Header("References")]
    public Camera worldCamera;                    // If null, defaults to Camera.main
    public Collider2D hoverCollider;              // If null, tries to find one on this object

    [Header("Idle Pulse")]
    [Tooltip("Base sine frequency (cycles per second) when not hovered.")]
    public float idleFrequency = 0.8f;
    [Tooltip("How much glow is added on top of the base GlowBoost when idle.")]
    public float idleIntensity = 0.5f;

    [Header("Hover Pulse")]
    [Tooltip("Pulse frequency while hovered.")]
    public float hoverFrequency = 2.0f;
    [Tooltip("Extra glow while hovered.")]
    public float hoverIntensity = 1.2f;

    [Header("Look")]
    [Tooltip("Sharper pulses with higher shape (>1). 1 = pure sine.")]
    public float shape = 1.25f;
    [Tooltip("How quickly we transition between idle/hover states.")]
    public float transitionSpeed = 10f;

    [Header("Optional Ring Throb")]
    [Tooltip("Scale the ring thickness a bit with the pulse (0 = off).")]
    [Range(0f, 0.5f)] public float ringThicknessPulse = 0.08f;

    SpriteRenderer _sr;
    MaterialPropertyBlock _mpb;
    CoinVisual _coin;                // optional; only used to read current base values
    float _hover01;                  // smoothed [0..1]
    float _time;                     // local phase accumulator

    // Cached property IDs
    static readonly int PID_GlowBoost = Shader.PropertyToID("_GlowBoost");
    static readonly int PID_RingThick = Shader.PropertyToID("_RingThick");

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
        _coin = GetComponent<CoinVisual>();
        if (!hoverCollider) hoverCollider = GetComponent<Collider2D>();
        if (!worldCamera) worldCamera = Camera.main;
    }

    void Update()
    {
        if (!_sr) return;

        // --- 1) Detect hover in world space ---
        bool hovered = IsPointerOverMe();

        // Smoothly blend between idle (0) and hover (1)
        float target = hovered ? 1f : 0f;
        _hover01 = Mathf.MoveTowards(_hover01, target, transitionSpeed * Time.deltaTime);

        // --- 2) Compute pulse params ---
        float freq = Mathf.Lerp(idleFrequency, hoverFrequency, _hover01);
        float intensity = Mathf.Lerp(idleIntensity, hoverIntensity, _hover01);

        _time += Time.deltaTime * Mathf.Max(0.0001f, freq);
        // Basic 0..1 sine pulse
        float s = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.PI * 2f);
        // Shape it (gamma-ish) to make peaks pop a bit
        float pulse01 = Mathf.Pow(Mathf.Clamp01(s), Mathf.Max(0.0001f, shape));

        // --- 3) Read current values, apply pulse on top ---
        // Base glow = either from CoinVisual or the material block
        float baseGlow = (_coin != null) ? _coin.glowBoost : ReadFloat(PID_GlowBoost, 1.5f);
        float baseRingThick = (_coin != null) ? _coin.ringThickness : ReadFloat(PID_RingThick, 0.07f);

        float boostedGlow = baseGlow + pulse01 * intensity;

        // Optional: throb ring thickness slightly
        float ringThick = baseRingThick * (1f + pulse01 * ringThicknessPulse);

        // --- 4) Write back to the renderer MPB (no new material instances) ---
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(PID_GlowBoost, boostedGlow);
        _mpb.SetFloat(PID_RingThick, ringThick);
        _sr.SetPropertyBlock(_mpb);
    }

    bool IsPointerOverMe()
    {
        if (!worldCamera || !hoverCollider) return false;

        // Mouse or primary touch position
        Vector3 screenPos = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : (Vector3)Input.mousePosition;
        Vector3 worldPos = worldCamera.ScreenToWorldPoint(screenPos);
        Vector2 p2 = new Vector2(worldPos.x, worldPos.y);

        // Use collider check for accurate shape; supports CircleCollider2D/Polygon/Box, etc.
        return hoverCollider.OverlapPoint(p2);
    }

    float ReadFloat(int propertyId, float fallback)
    {
        _sr.GetPropertyBlock(_mpb);
        // There isn't a direct "get" API for MPB floats; we’ll just return fallback.
        // Using CoinVisual for base values avoids this anyway.
        return fallback;
    }
}
