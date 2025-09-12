using UnityEngine;
using UnityEngine.UI;

public class CoinDragSync : MonoBehaviour
{
    [Header("Identity / Key")]
    public uint ownerNetId;     // filled by spawner (already done in your code)
    public int slotIndex = -1;  // we’ll set this in the spawner
    [SerializeField] string _key; // debug view

    [Header("Refs")]
    [SerializeField] RectTransform rt;
    [SerializeField] CanvasGroup cg;          // to disable raycasts while dragging
    [SerializeField] DraggableCoin draggable; // to access dragLayer/rootCanvas

    void Awake()
    {
        if (!rt) rt = transform as RectTransform;
        if (!cg) cg = GetComponent<CanvasGroup>();
        if (!draggable) draggable = GetComponent<DraggableCoin>();
    }

    void OnEnable() { TryRegister(); }
    void OnDisable() { TryUnregister(); }

    void TryRegister()
    {
        _key = BuildKey(ownerNetId, slotIndex);
        if (CoinDragRelay.Instance) CoinDragRelay.Register(_key, this);
    }

    void TryUnregister()
    {
        if (CoinDragRelay.Instance) CoinDragRelay.Unregister(_key, this);
    }

    static string BuildKey(uint owner, int slot) => $"{owner}:{slot}";

    // ---------------- called by DraggableCoin (local owner only) ----------------

    public void OwnerBeginDrag()
    {
        if (!CoinDragRelay.Instance) return;
        CoinDragRelay.Instance.CmdBegin(_key, ownerNetId);
    }

    public void OwnerUpdateDrag(Vector2 anchored)
    {
        if (!CoinDragRelay.Instance) return;
        CoinDragRelay.Instance.CmdUpdate(_key, anchored);
    }

    // parentPath is relative to the root canvas; we’ll compute it in DraggableCoin when dropping.
    public void OwnerEndDrag(Vector2 anchored, string parentPath)
    {
        if (!CoinDragRelay.Instance) return;
        CoinDragRelay.Instance.CmdEnd(_key, anchored, parentPath);
    }

    // ---------------- incoming network updates (apply on non-owners) ----------------

    public void RemoteBeginDrag()
    {
        if (!cg) return;
        cg.blocksRaycasts = false;

        // Move on top while being dragged so it visually overlaps settled coins.
        if (draggable && draggable.dragLayer)
            rt.SetParent(draggable.dragLayer, worldPositionStays: true);
    }

    public void RemoteUpdateDrag(Vector2 anchored)
    {
        if (rt) rt.anchoredPosition = anchored;
    }

    public void RemoteEndDrag(Vector2 anchored, string parentPath)
    {
        if (!rt) return;

        // Try to reparent to the exact same snap anchor as the owner used:
        var parent = FindTransformByPath(GetRootForPath(), parentPath);
        if (parent)
        {
            rt.SetParent(parent as RectTransform, worldPositionStays: false);
            rt.anchoredPosition = Vector2.zero; // snapped
        }
        else
        {
            // Fallback: keep whatever parent we had and place by anchored position
            rt.anchoredPosition = anchored;
        }

        if (cg) cg.blocksRaycasts = true;
    }

    // ---------------- path helpers ----------------

    Transform GetRootForPath()
    {
        // We’ll use the root canvas as a stable root; matches how we build the path in DraggableCoin
        return draggable && draggable.rootCanvas ? draggable.rootCanvas.transform : transform.root;
    }

    static Transform FindTransformByPath(Transform root, string path)
    {
        if (!root || string.IsNullOrEmpty(path)) return null;
        var current = root;
        var parts = path.Split('/');
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            bool found = false;
            for (int i = 0; i < current.childCount; i++)
            {
                var c = current.GetChild(i);
                if (c.name == part) { current = c; found = true; break; }
            }
            if (!found) return null;
        }
        return current;
    }
}
