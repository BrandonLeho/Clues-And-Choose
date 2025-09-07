using UnityEngine;

[ExecuteAlways]
public class ViewportAnchor : MonoBehaviour
{
    public enum Anchor
    {
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private Anchor anchor = Anchor.TopCenter;
    [SerializeField] private float distanceFromCamera = 5f; // world units
    [SerializeField] private Vector2 worldOffset = new Vector2(0f, -0.2f); // x = right, y = up in camera space
    [SerializeField] private bool faceCamera = false;

    void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        Vector2 vp = AnchorToViewport(anchor);
        Vector3 baseWorld = targetCamera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, distanceFromCamera));

        // Offset in camera X/Y directions
        Vector3 offset = targetCamera.transform.right * worldOffset.x + targetCamera.transform.up * worldOffset.y;

        transform.position = baseWorld + offset;
        if (faceCamera)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position, Vector3.up);
        }
    }

    private Vector2 AnchorToViewport(Anchor a)
    {
        switch (a)
        {
            case Anchor.TopLeft: return new Vector2(0f, 1f);
            case Anchor.TopCenter: return new Vector2(0.5f, 1f);
            case Anchor.TopRight: return new Vector2(1f, 1f);
            case Anchor.MiddleLeft: return new Vector2(0f, 0.5f);
            case Anchor.MiddleCenter: return new Vector2(0.5f, 0.5f);
            case Anchor.MiddleRight: return new Vector2(1f, 0.5f);
            case Anchor.BottomLeft: return new Vector2(0f, 0f);
            case Anchor.BottomCenter: return new Vector2(0.5f, 0f);
            case Anchor.BottomRight: return new Vector2(1f, 0f);
        }
        return new Vector2(0.5f, 1f);
    }
}
