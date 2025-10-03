using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public sealed class EndRoundOptionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum OptionKind { Yes, No }

    [Header("What am I?")]
    public OptionKind kind = OptionKind.Yes;

    [Header("Hover Scale")]
    [Range(1.0f, 1.6f)] public float hoverScale = 1.08f;
    [Range(0.05f, 0.6f)] public float scaleDuration = 0.18f;
    public AnimationCurve scaleEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Outline (Hover)")]
    [Range(0f, 1f)] public float outlineWidth = 0.35f;
    [Range(0f, 1f)] public float outlineSoftness = 0.2f;
    public Color yesOutline = new Color(0.25f, 1f, 0.25f, 1f);
    public Color noOutline = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Glow (Hover)")]
    public bool enableGlow = true;
    [Range(-1f, 1f)] public float glowOffset = 0f;
    [Range(0f, 1f)] public float glowInner = 1f;
    [Range(0f, 1f)] public float glowOuter = 0.35f;
    [Range(0f, 1.5f)] public float glowPower = 0.9f;

    TMP_Text _label;
    Vector3 _baseScale;
    Coroutine _scaleCo;

    void Awake()
    {
        _label = GetComponent<TMP_Text>();
        _baseScale = transform.localScale;
        ClearOutline(_label);
        ClearGlow(_label);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayScale(true);
        var col = (kind == OptionKind.Yes) ? yesOutline : noOutline;
        SetOutline(_label, col, outlineWidth, outlineSoftness);
        if (enableGlow) SetGlow(_label, col, glowOffset, glowInner, glowOuter, glowPower);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PlayScale(false);
        ClearOutline(_label);
        ClearGlow(_label);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        EndRoundPromptUI.Instance?.PlayChoiceExit(kind);
    }

    void PlayScale(bool toHover)
    {
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        _scaleCo = StartCoroutine(CoScale(toHover));
    }

    IEnumerator CoScale(bool toHover)
    {
        Vector3 from = transform.localScale;
        Vector3 to = toHover ? (_baseScale * hoverScale) : _baseScale;

        float t = 0f;
        float dur = Mathf.Max(0.0001f, scaleDuration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float e = scaleEase.Evaluate(Mathf.Clamp01(t));
            transform.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }
        transform.localScale = to;
        _scaleCo = null;
    }

    public IEnumerator PlayImpactBurst(float impactScale = 1.18f, float impactDuration = 0.18f, float extraGlow = 0.4f)
    {
        var col = (kind == OptionKind.Yes) ? yesOutline : noOutline;

        SetOutline(_label, col, outlineWidth + 0.15f, Mathf.Clamp01(outlineSoftness + 0.1f));
        if (enableGlow) SetGlow(_label, col, glowOffset, glowInner, glowOuter + extraGlow, glowPower + 0.4f);

        Vector3 from = transform.localScale;
        Vector3 to = _baseScale * impactScale;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, impactDuration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }
        transform.localScale = to;
    }

    public void ResetVisuals()
    {
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        transform.localScale = _baseScale;
        ClearOutline(_label);
        ClearGlow(_label);
    }

    void SetOutline(TMP_Text t, Color col, float width, float softness)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, softness);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, col);
        t.fontMaterial = mat;
    }

    void ClearOutline(TMP_Text t)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        t.fontMaterial = mat;
    }

    void SetGlow(TMP_Text t, Color col, float offset, float inner, float outer, float power)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
        mat.SetColor(ShaderUtilities.ID_GlowColor, col);
        mat.SetFloat(ShaderUtilities.ID_GlowOffset, offset);
        mat.SetFloat(ShaderUtilities.ID_GlowInner, inner);
        mat.SetFloat(ShaderUtilities.ID_GlowOuter, outer);
        mat.SetFloat(ShaderUtilities.ID_GlowPower, power);
        t.fontMaterial = mat;
    }

    void ClearGlow(TMP_Text t)
    {
        if (!t) return;
        var mat = t.fontMaterial;
        mat.DisableKeyword(ShaderUtilities.Keyword_Glow);
        mat.SetFloat(ShaderUtilities.ID_GlowInner, 0f);
        mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0f);
        mat.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
        t.fontMaterial = mat;
    }
}
