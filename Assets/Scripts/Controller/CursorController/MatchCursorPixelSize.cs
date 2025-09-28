using UnityEngine;

public class MatchCursorPixelSize : MonoBehaviour
{
    [SerializeField] SpriteRenderer targetSR;
    [SerializeField] Camera cam;
    [SerializeField] float atZ = 0f;

    int _lastScrW, _lastScrH;
    Texture2D _lastTex;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (CursorControllerModule.Instance != null)
            CursorControllerModule.Instance.OnCursorVisualChanged += UpdateScaleFromCursor;
    }

    void OnDestroy()
    {
        if (CursorControllerModule.Instance != null)
            CursorControllerModule.Instance.OnCursorVisualChanged -= UpdateScaleFromCursor;
    }

    void Start() { UpdateScaleFromCursor(); }

    void Update()
    {
        if (Screen.width != _lastScrW || Screen.height != _lastScrH)
            UpdateScaleFromCursor();
    }

    void UpdateScaleFromCursor()
    {
        _lastScrW = Screen.width; _lastScrH = Screen.height;

        if (!targetSR || targetSR.sprite == null || cam == null) return;

        var tex = CursorControllerModule.Instance ? CursorControllerModule.Instance.CurrentTexture : null;
        int desiredPxW = tex ? tex.width : 32;
        int desiredPxH = tex ? tex.height : 32;

        float scale = ComputeUniformScaleToMatchPixels(targetSR.sprite, cam, desiredPxW, desiredPxH, atZ);
        var ls = targetSR.transform.localScale;
        targetSR.transform.localScale = new Vector3(scale, scale, ls.z);

        _lastTex = tex;
    }

    static float ComputeUniformScaleToMatchPixels(Sprite sprite, Camera cam, int pxW, int pxH, float z)
    {
        float spriteWorldW = sprite.rect.width / sprite.pixelsPerUnit;
        float spriteWorldH = sprite.rect.height / sprite.pixelsPerUnit;

        float ppwu = PixelsPerWorldUnit(cam, z);
        float targetWorldW = pxW / ppwu;
        float targetWorldH = pxH / ppwu;

        float sx = targetWorldW / spriteWorldW;
        float sy = targetWorldH / spriteWorldH;

        return Mathf.Min(sx, sy);
    }

    static float PixelsPerWorldUnit(Camera cam, float z)
    {
        if (cam.orthographic)
            return Screen.height / (2f * cam.orthographicSize);

        var p0 = cam.WorldToScreenPoint(new Vector3(0f, 0f, z));
        var p1 = cam.WorldToScreenPoint(new Vector3(1f, 0f, z));
        return Mathf.Abs(p1.x - p0.x);
    }
}
