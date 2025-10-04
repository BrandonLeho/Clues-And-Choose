using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class TMPOutlineGlow : MonoBehaviour
{
    [SerializeField] TMPGlowOutlineConfig config = new TMPGlowOutlineConfig();

    bool _usePerInstanceColor;
    Color _perInstanceColor;

    TMP_Text _tmp;

    void Awake()
    {
        _tmp = GetComponent<TMP_Text>();
        Apply();
    }

    void OnEnable() => Apply();
    void OnValidate() { if (isActiveAndEnabled) Apply(); }

    public void SetConfig(TMPGlowOutlineConfig newConfig, bool applyNow = true)
    {
        if (newConfig != null) config = newConfig;
        if (applyNow) Apply();
    }

    public void SetPerInstanceExplicitColor(Color c, bool applyNow = true)
    {
        _usePerInstanceColor = true;
        _perInstanceColor = c;
        if (applyNow) Apply();
    }

    public void ClearPerInstanceExplicitColor(bool applyNow = true)
    {
        _usePerInstanceColor = false;
        if (applyNow) Apply();
    }

    public void Apply()
    {
        if (!_tmp || config == null) return;

        var mat = _tmp.fontMaterial;

        Color col;
        if (_usePerInstanceColor)
        {
            col = _perInstanceColor;
        }
        else
        {
            col = (config.colorMode == TMPGlowOutlineConfig.ColorMode.UseLabelColor)
                ? _tmp.color
                : config.explicitColor;
        }

        if (config.enableOutline)
        {
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, config.outlineWidth);
            mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, config.outlineSoftness);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, col);
        }
        else
        {
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        }

        if (config.enableGlow)
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
            mat.SetColor(ShaderUtilities.ID_GlowColor, col);
            mat.SetFloat(ShaderUtilities.ID_GlowOffset, config.glowOffset);
            mat.SetFloat(ShaderUtilities.ID_GlowInner, config.glowInner);
            mat.SetFloat(ShaderUtilities.ID_GlowOuter, config.glowOuter);
            mat.SetFloat(ShaderUtilities.ID_GlowPower, config.glowPower);
        }
        else
        {
            mat.DisableKeyword(ShaderUtilities.Keyword_Glow);
            mat.SetFloat(ShaderUtilities.ID_GlowInner, 0f);
            mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0f);
            mat.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
        }

        _tmp.fontMaterial = mat;
    }
}
