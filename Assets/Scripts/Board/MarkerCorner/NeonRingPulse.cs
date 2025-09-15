using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
public class NeonRingPulse : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Pulse")]
    [Tooltip("Always pulse (set false if you only want it on hover).")]
    public bool pulseAlways = true;
    [Tooltip("Also pulse when hovered.")]
    public bool pulseOnHover = true;
    [Range(0.5f, 10f)] public float baseIntensity = 2.2f;
    [Range(0f, 1.0f)] public float pulseAmplitude = 0.25f;
    [Range(0.05f, 5f)] public float pulseSpeed = 1.3f;
    public bool useUnscaledTime = true;
    public bool randomizePhase = true;

    Material _mat;
    bool _hover;
    float _phase;

    void Awake()
    {
        var img = GetComponent<Image>();
        _mat = Instantiate(img.material);
        img.material = _mat;

        if (randomizePhase) _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void OnEnable()
    {
        if (_mat) _mat.SetFloat("_Intensity", baseIntensity);
    }

    void OnDestroy()
    {
        if (_mat) Destroy(_mat);
    }

    void Update()
    {
        if (_mat == null) return;

        bool active = pulseAlways || (pulseOnHover && _hover);
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;

        float k = active ? (1f + pulseAmplitude * Mathf.Sin(t * Mathf.PI * 2f * pulseSpeed + _phase)) : 1f;

        _mat.SetFloat("_Intensity", baseIntensity * k);
    }

    public void OnPointerEnter(PointerEventData e) => _hover = true;
    public void OnPointerExit(PointerEventData e) => _hover = false;
}
