using UnityEngine;

[System.Serializable]
public class TMPGlowOutlineConfig
{
    public enum ColorMode { UseLabelColor, Explicit }

    [Header("Color")]
    public ColorMode colorMode = ColorMode.UseLabelColor;
    public Color explicitColor = Color.white;

    [Header("Outline")]
    public bool enableOutline = true;
    [Range(0f, 1f)] public float outlineWidth = 0.25f;
    [Range(0f, 1f)] public float outlineSoftness = 0.15f;

    [Header("Glow")]
    public bool enableGlow = true;
    [Range(-1f, 1f)] public float glowOffset = 0f;
    [Range(0f, 1f)] public float glowInner = 1f;
    [Range(0f, 1f)] public float glowOuter = 0.25f;
    [Range(0f, 1.5f)] public float glowPower = 0.75f;
}
