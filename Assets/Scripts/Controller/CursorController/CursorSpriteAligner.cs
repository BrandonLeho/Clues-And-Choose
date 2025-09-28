using UnityEngine;

[ExecuteAlways]
public class CursorSpriteAligner : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] SpriteRenderer targetRenderer;
    [SerializeField] Camera targetCamera;

    [Header("Sizing")]
    [SerializeField] bool matchLocalCursorTexture = true;
    [SerializeField] int fallbackTargetPixelHeight = 32;
    [SerializeField] float viewerScaleFudge = 1f;

    [Header("Hotspot")]
    [SerializeField, Range(0f, 1f)] Vector2 hotspotNormalized = new Vector2(0f, 1f);

    [Header("Extra Offsets")]
    [SerializeField] Vector2 offsetPixels = Vector2.zero;
    [SerializeField] Vector2 offsetWorld = Vector2.zero;

    int _targetPixels;

    void OnEnable()
    {
        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        if (!targetCamera) targetCamera = Camera.main;

        _targetPixels = fallbackTargetPixelHeight;

        if (matchLocalCursorTexture && CursorControllerModule.CurrentTexture != null)
            _targetPixels = CursorControllerModule.CurrentTexture.height;

        if (matchLocalCursorTexture)
            CursorControllerModule.OnCursorTextureChanged += HandleCursorChanged;

        Refresh();
    }

    void OnDisable()
    {
        if (matchLocalCursorTexture)
            CursorControllerModule.OnCursorTextureChanged -= HandleCursorChanged;
    }

    void LateUpdate()
    {
        Refresh();
    }

    void HandleCursorChanged(Texture2D tex)
    {
        if (tex != null)
        {
            _targetPixels = tex.height;
            Refresh();
        }
    }

    void Refresh()
    {
        if (!targetRenderer || !targetRenderer.sprite || targetCamera == null) return;

        float pxPerWU = PixelsPerWorldUnitAt(targetCamera, transform.position);
        float desiredWorldH = _targetPixels * Mathf.Max(0.0001f, viewerScaleFudge) / Mathf.Max(pxPerWU, 0.0001f);
        float spriteH_World_Unscaled = targetRenderer.sprite.rect.height / targetRenderer.sprite.pixelsPerUnit;
        if (spriteH_World_Unscaled <= 0f) return;

        float uniformScale = desiredWorldH / spriteH_World_Unscaled;

        var ls = transform.localScale;
        ls.x = ls.y = uniformScale;
        transform.localScale = ls;

        Vector2 spriteWorldSize = new Vector2(
            targetRenderer.sprite.rect.width / targetRenderer.sprite.pixelsPerUnit * uniformScale,
            targetRenderer.sprite.rect.height / targetRenderer.sprite.pixelsPerUnit * uniformScale
        );

        Vector2 pivotNorm = targetRenderer.sprite.pivot;
        Vector2 rectSize = targetRenderer.sprite.rect.size;
        pivotNorm.x /= Mathf.Max(1f, rectSize.x);
        pivotNorm.y /= Mathf.Max(1f, rectSize.y);

        Vector2 deltaNorm = hotspotNormalized - pivotNorm;
        Vector2 deltaWorld = new Vector2(deltaNorm.x * spriteWorldSize.x,
                                          deltaNorm.y * spriteWorldSize.y);

        Vector2 nudgeWorld = new Vector2(offsetPixels.x / Mathf.Max(pxPerWU, 0.0001f),
                                         offsetPixels.y / Mathf.Max(pxPerWU, 0.0001f));

        Vector3 lp = Vector3.zero;
        lp.x = deltaWorld.x + nudgeWorld.x + offsetWorld.x;
        lp.y = deltaWorld.y + nudgeWorld.y + offsetWorld.y;
        transform.localPosition = lp;
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
