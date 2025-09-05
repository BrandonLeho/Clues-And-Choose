using UnityEngine;

/// Attach to a GameObject with a SpriteRenderer to visually represent the cursor.
/// For 2D projects, leave Z = 0 (or set a custom zDepth if needed).
[DefaultExecutionOrder(100)]
public class ChangeCursor : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 worldOffset = Vector2.zero; // tweak if your sprite pivot isn't at the "tip"
    [SerializeField] private float zDepth = 0f;                  // where the sprite sits in world space

    private void Awake()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!targetCamera) targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        // Hide hardware cursor; we’ll draw our own.
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDisable()
    {
        // Restore hardware cursor visibility if this visual is disabled/destroyed.
        Cursor.visible = true;
    }

    private void LateUpdate()
    {
        if (!targetCamera) return;

        Vector3 mp = Input.mousePosition;
        mp.z = Mathf.Abs(zDepth - targetCamera.transform.position.z); // for perspective cams
        Vector3 world = targetCamera.ScreenToWorldPoint(mp);
        world.z = zDepth;
        transform.position = world + (Vector3)worldOffset;
    }

    public void SetSprite(Sprite s)
    {
        if (spriteRenderer) spriteRenderer.sprite = s;
    }

    public void SetColor(Color c)
    {
        if (spriteRenderer) spriteRenderer.color = c;
    }
}
