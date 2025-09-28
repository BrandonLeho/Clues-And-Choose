using UnityEngine;

[ExecuteAlways]
public class CursorSpritePixelScaler : MonoBehaviour
{
    [SerializeField] SpriteRenderer targetRenderer;
    [SerializeField] Camera targetCamera;
    [SerializeField] bool matchLocalCursorTexture = true;
    [SerializeField] int fallbackTargetPixelHeight = 32;
    [SerializeField] float viewerScaleFudge = 1f;

    int _targetPixels;

    void OnEnable()
    {
        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        if (!targetCamera) targetCamera = Camera.main;

        _targetPixels = fallbackTargetPixelHeight;

        var m = CursorControllerModule.Instance;
        if (matchLocalCursorTexture && CursorControllerModule.CurrentTexture != null)
            _targetPixels = CursorControllerModule.CurrentTexture.height;

        if (matchLocalCursorTexture)
            CursorControllerModule.OnCursorTextureChanged += HandleCursorChanged;

        UpdateScale();
    }

    void OnDisable()
    {
        if (matchLocalCursorTexture)
            CursorControllerModule.OnCursorTextureChanged -= HandleCursorChanged;
    }

    void LateUpdate()
    {
        UpdateScale();
    }

    void HandleCursorChanged(Texture2D tex)
    {
        if (tex != null)
        {
            _targetPixels = tex.height;
            UpdateScale();
        }
    }

    void UpdateScale()
    {
        if (!targetRenderer || !targetRenderer.sprite || targetCamera == null) return;

        float pxPerWorldUnit = PixelsPerWorldUnitAt(targetCamera, transform.position);
        float desiredWorldHeight = _targetPixels * viewerScaleFudge / pxPerWorldUnit;
        float spriteWorldHeightUnscaled = targetRenderer.sprite.rect.height / targetRenderer.sprite.pixelsPerUnit;

        if (spriteWorldHeightUnscaled <= 0f) return;

        float uniform = desiredWorldHeight / spriteWorldHeightUnscaled;

        var ls = transform.localScale;
        ls.x = ls.y = uniform;
        transform.localScale = ls;
    }

    static float PixelsPerWorldUnitAt(Camera cam, Vector3 worldPos)
    {
        if (cam.orthographic)
        {
            return Screen.height / (2f * cam.orthographicSize);
        }
        else
        {
            float dist = Vector3.Dot(worldPos - cam.transform.position, cam.transform.forward);
            float worldHeight = 2f * Mathf.Abs(dist) * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad);
            return Screen.height / Mathf.Max(worldHeight, 1e-4f);
        }
    }
}
