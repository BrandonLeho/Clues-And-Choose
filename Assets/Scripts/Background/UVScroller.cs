using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UVScroller : MonoBehaviour
{
    [SerializeField] private RawImage target;
    [SerializeField] private Vector2 uvTiling = new Vector2(6, 6);
    [SerializeField] private float speed = 0.02f; // units per second along X of the RawImage
    [SerializeField] private bool useUnscaledTime = true;

    private Vector2 uvOffset;

    void Reset() { target = GetComponent<RawImage>(); }

    void OnEnable() { Apply(); }

    void Update()
    {
        if (!target || target.texture == null) return;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        uvOffset.x += speed * dt;              // scroll along RawImage local X
        Apply();
    }

    void Apply()
    {
        // Tiling is handled by uvRect width/height > 1
        target.uvRect = new Rect(uvOffset, uvTiling);
    }
}
