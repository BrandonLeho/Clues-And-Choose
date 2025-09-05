using UnityEngine;

[CreateAssetMenu(menuName = "HuesAndCues/ColorBoard2D")]
public class ColorBoard2D : ScriptableObject
{
    public Texture2D boardTexture;
    [Min(1)] public int cols = 30;
    [Min(1)] public int rows = 16;

    public Color ColorAt(int x, int y)
    {
        if (!boardTexture) return Color.white;
        float u = (x + 0.5f) / cols;
        float v = (y + 0.5f) / rows;
        return boardTexture.GetPixelBilinear(u, v);
    }
}
