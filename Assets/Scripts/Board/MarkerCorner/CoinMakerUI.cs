using UnityEngine;
using UnityEngine.UI;

public class CoinMakerUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Image fill;
    [SerializeField] Image neonRing;
    [SerializeField] Image shadow;

    [Header("Style")]
    [SerializeField] Color playerColor = Color.cyan;
    [SerializeField, Range(0f, 1f)] float faceDarken = 0.15f;
    [SerializeField, Range(0f, 10f)] float glowIntensity = 2.5f;
    [SerializeField, Range(0.02f, 0.3f)] float ringThickness = 0.12f;
    [SerializeField, Range(0f, 0.3f)] float ringSoftness = 0.10f;
    [SerializeField, Range(0f, 1f)] float alpha = 1f;

    [Header("Feedback")]
    [SerializeField] bool pulseOnHover = true;
    [SerializeField, Range(0f, 1f)] float hoverScale = 1.08f;
    [SerializeField, Range(0.1f, 4f)] float pulseSpeed = 1.8f;

    Material _ringMat;
    Vector3 _baseScale;
    bool _hover;

    void Awake()
    {
        _baseScale = transform.localScale;
        if (neonRing && neonRing.material) _ringMat = Instantiate(neonRing.material); // no shared edits
        if (neonRing) neonRing.material = _ringMat;
        ApplyStyle();
    }

    void Update()
    {
        if (pulseOnHover && _hover && _ringMat)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * pulseSpeed);
            _ringMat.SetFloat("_Intensity", glowIntensity * (1f + 0.25f * pulse));
        }
    }

    public void SetPlayerColor(Color c)
    {
        playerColor = c;
        ApplyStyle();
    }

    public void SetHover(bool on)
    {
        _hover = on;
        transform.localScale = on ? _baseScale * hoverScale : _baseScale;
        if (_ringMat) _ringMat.SetFloat("_Intensity", glowIntensity);
    }

    void ApplyStyle()
    {
        if (fill) fill.color = Color.Lerp(Color.black, playerColor, faceDarken);
        if (_ringMat)
        {
            _ringMat.SetColor("_Color", playerColor.linear);
            _ringMat.SetFloat("_Intensity", glowIntensity);
            _ringMat.SetFloat("_Thickness", ringThickness);
            _ringMat.SetFloat("_Softness", ringSoftness);
            _ringMat.SetFloat("_Alpha", alpha);
        }
        if (shadow) shadow.color = new Color(0, 0, 0, 0.25f);
    }

    // optional placement flash
    public void FlashOnPlace(float extraIntensity = 3f, float duration = 0.15f)
    {
        if (!gameObject.activeInHierarchy || _ringMat == null) return;
        StartCoroutine(Flash());

        System.Collections.IEnumerator Flash()
        {
            float t = 0f;
            float start = glowIntensity;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = 1f - (t / duration);
                _ringMat.SetFloat("_Intensity", Mathf.Lerp(start + extraIntensity, start, 1f - p * p));
                yield return null;
            }
            _ringMat.SetFloat("_Intensity", start);
        }
    }
}
