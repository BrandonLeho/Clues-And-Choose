using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BoardSpriteGenerator2D : MonoBehaviour
{
    public PaletteGrid palette;   // <— changed type
    public int pixelsPerCell = 16;
    public bool drawGridLines = true;
    [Range(0, 4)] public int lineThickness = 1;
    public Color lineColor = new Color(0f, 0f, 0f, 0.25f);

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (!palette) { Debug.LogError("PaletteGrid missing"); return; }

        int wCells = palette.cols, hCells = palette.rows;
        int cellW = pixelsPerCell, cellH = pixelsPerCell;
        int w = wCells * cellW + (drawGridLines ? (wCells + 1) * lineThickness : 0);
        int h = hCells * cellH + (drawGridLines ? (hCells + 1) * lineThickness : 0);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = drawGridLines ? lineColor : Color.clear;

        void FillRect(int x, int y, int rw, int rh, Color c)
        {
            for (int yy = 0; yy < rh; yy++)
            {
                int row = (y + yy) * w + x;
                for (int xx = 0; xx < rw; xx++) px[row + xx] = c;
            }
        }
        int CellX(int cx) => drawGridLines ? (cx * cellW + (cx + 1) * lineThickness) : (cx * cellW);
        int CellY(int cy) => drawGridLines ? (cy * cellH + (cy + 1) * lineThickness) : (cy * cellH);

        for (int y = 0; y < hCells; y++)
            for (int x = 0; x < wCells; x++)
            {
                var c = palette.ColorAt(x, y);
                FillRect(CellX(x), CellY(y), cellW, cellH, c);
            }

        if (drawGridLines)
        {
            for (int gx = 0; gx <= wCells; gx++) FillRect(gx * cellW + gx * lineThickness, 0, lineThickness, h, lineColor);
            for (int gy = 0; gy <= hCells; gy++) FillRect(0, gy * cellH + gy * lineThickness, w, lineThickness, lineColor);
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerCell);
        sr.sprite = sprite;
    }
}
