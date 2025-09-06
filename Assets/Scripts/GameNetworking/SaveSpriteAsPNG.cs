using UnityEngine;
using System.IO;

public class SaveSpriteAsPNG : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public string fileName = "SavedSprite.png";

    [ContextMenu("Save Sprite (No Read/Write Needed)")]
    public void SaveSprite()
    {
        var sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (sprite == null || sprite.texture == null)
        {
            Debug.LogError("[SaveSpriteAsPNG] No sprite/texture found.");
            return;
        }

        // Sprite sub-rect in the source texture (handles sliced sprites too)
        Rect tr = sprite.textureRect;
        int width = Mathf.RoundToInt(tr.width);
        int height = Mathf.RoundToInt(tr.height);

        // 1) Make a temp RT sized to the sprite rect
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        var prevRT = RenderTexture.active;
        RenderTexture.active = rt;

        // 2) Draw ONLY the sprite's rect from the source texture into the RT
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, width, 0, height);
        // Normalized source-UV rect
        var tex = sprite.texture;
        Rect srcUV = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        // Fill the whole RT
        Rect dstPx = new Rect(0, 0, width, height);
        Graphics.DrawTexture(dstPx, tex, srcUV, 0, 0, 0, 0);
        GL.PopMatrix();

        // 3) Read pixels from the RT (now we have CPU pixels without needing Read/Write on the source)
        Texture2D outTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        outTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        outTex.Apply();

        // 4) Encode and save
        byte[] png = outTex.EncodeToPNG();
        string path = Path.Combine(Application.dataPath, fileName);
        File.WriteAllBytes(path, png);
        Debug.Log("[SaveSpriteAsPNG] Saved to: " + path);

        // Cleanup
        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(outTex);
    }
}
