using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class CoinPulseController : MonoBehaviour
{
    [Header("References")]
    public Camera worldCamera;
    public Collider2D hoverCollider;

    [Header("Idle Pulse")]
    public float idleFrequency = 0.8f;
    public float idleIntensity = 0.5f;

    [Header("Hover Pulse")]
    public float hoverFrequency = 2.0f;
    public float hoverIntensity = 1.2f;

    [Header("Look")]
    public float shape = 1.25f;
    public float transitionSpeed = 10f;

    [Header("Optional Ring Throb")]
    [Range(0f, 0.5f)] public float ringThicknessPulse = 0.08f;

    [Header("Hover Scale Effect")]
    [Range(1f, 1.5f)] public float hoverScaleMultiplier = 1.1f;
    public float scaleLerpSpeed = 10f;

    [Header("Hover Scale Eligibility")]
    public bool requireLocalOwnershipForHoverScale = true;
    public bool blockHoverScaleIfLocalIsClueGiver = true;

    SpriteRenderer _sr;
    MaterialPropertyBlock _mpb;
    CoinVisual _coin;
    float _hover01;
    float _time;
    Vector3 _baseScale;
    NetworkCoin _net;
    CoinPlacedLock _placedLock;

    static readonly int PID_GlowBoost = Shader.PropertyToID("_GlowBoost");
    static readonly int PID_RingThick = Shader.PropertyToID("_RingThick");

    void Awake()
    {
        _net = GetComponentInParent<NetworkCoin>();
        _sr = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
        _coin = GetComponent<CoinVisual>();
        _baseScale = transform.localScale;

        _placedLock = GetComponentInParent<CoinPlacedLock>();

        if (!hoverCollider) hoverCollider = GetComponentInParent<Collider2D>();
        if (!worldCamera) worldCamera = Camera.main;
    }

    void Update()
    {
        if (!_sr) return;

        bool hovered = IsPointerOverMe();
        float target = hovered ? 1f : 0f;
        _hover01 = Mathf.MoveTowards(_hover01, target, transitionSpeed * Time.deltaTime);

        float freq = Mathf.Lerp(idleFrequency, hoverFrequency, _hover01);
        float intensity = Mathf.Lerp(idleIntensity, hoverIntensity, _hover01);

        _time += Time.deltaTime * Mathf.Max(0.0001f, freq);
        float s = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.PI * 2f);
        float pulse01 = Mathf.Pow(Mathf.Clamp01(s), Mathf.Max(0.0001f, shape));

        float baseGlow = (_coin != null) ? _coin.glowBoost : ReadFloat(PID_GlowBoost, 1.5f);
        float baseRingThick = (_coin != null) ? _coin.ringThickness : ReadFloat(PID_RingThick, 0.07f);

        float boostedGlow = baseGlow + pulse01 * intensity;
        float ringThick = baseRingThick * (1f + pulse01 * ringThicknessPulse);

        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(PID_GlowBoost, boostedGlow);
        _mpb.SetFloat(PID_RingThick, ringThick);
        _sr.SetPropertyBlock(_mpb);

        bool canScale = (!requireLocalOwnershipForHoverScale) || (_net != null && _net.IsLocalOwner());

        if (blockHoverScaleIfLocalIsClueGiver && canScale)
        {
            if (ClueGiverState.IsLocalPlayerClueGiver()) canScale = false;
        }

        if (_placedLock != null && _placedLock.locked)
        {
            canScale = false;
        }

        float targetScale = canScale ? Mathf.Lerp(1f, hoverScaleMultiplier, _hover01) : 1f;

        transform.localScale = Vector3.Lerp(
            transform.localScale, _baseScale * targetScale,
            scaleLerpSpeed * Time.deltaTime);
    }

    bool IsPointerOverMe()
    {
        if (!worldCamera || !hoverCollider) return false;

        Vector3 screenPos = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : (Vector3)Input.mousePosition;
        Vector3 worldPos = worldCamera.ScreenToWorldPoint(screenPos);
        Vector2 p2 = new Vector2(worldPos.x, worldPos.y);

        return hoverCollider.OverlapPoint(p2);
    }

    float ReadFloat(int propertyId, float fallback)
    {
        _sr.GetPropertyBlock(_mpb);
        return fallback;
    }
}
