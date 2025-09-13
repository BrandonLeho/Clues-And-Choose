using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class CoinDragSync : NetworkBehaviour
{
    [Header("UI Refs")]
    [SerializeField] RectTransform coinRT;
    [SerializeField] CanvasGroup coinCanvasGroup;

    [Header("Shared Roots (must exist on all clients)")]
    [Tooltip("Root used to resolve/encode Transform paths for parenting (eg. your root Canvas)")]
    [SerializeField] Transform pathRoot;

    [Tooltip("Where coins should temporarily live while being dragged (sits above the board)")]
    [SerializeField] Transform dragLayer;

    [Header("Ownership")]
    [SyncVar] public uint ownerNetId; // set by spawner / DraggableCoin


    void Awake()
    {
        if (!pathRoot)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas) pathRoot = canvas.transform;
        }

        if (!dragLayer)
        {
            var layerObj = GameObject.FindWithTag("DragLayer");
            if (layerObj) dragLayer = layerObj.transform;
        }
    }



    public void OwnerBeginDrag()
    {
        if (!NetworkClient.active) return;
        CmdBeginDrag();
    }

    public void OwnerUpdateDrag(Vector2 anchoredPos)
    {
        if (!NetworkClient.active) return;
        CmdUpdatePos(anchoredPos);
    }

    public void OwnerEndDrag(Vector2 anchoredPos)
    {
        if (!NetworkClient.active) return;

        // Try to capture the final parent path, so all clients can reparent identically.
        string parentPath = EncodePath(coinRT ? coinRT.parent : null);
        CmdEndDrag(anchoredPos, parentPath);
    }

    // --- Commands: allow without authority, but validate sender is the owner ---
    [Command(requiresAuthority = false)]
    void CmdBeginDrag(NetworkConnectionToClient sender = null)
    {
        if (!IsValidSender(sender)) return;
        RpcBeginDrag();
    }

    [Command(requiresAuthority = false)]
    void CmdUpdatePos(Vector2 anchoredPos, NetworkConnectionToClient sender = null)
    {
        if (!IsValidSender(sender)) return;
        RpcUpdatePos(anchoredPos);
    }

    [Command(requiresAuthority = false)]
    void CmdEndDrag(Vector2 anchoredPos, string parentPath, NetworkConnectionToClient sender = null)
    {
        if (!IsValidSender(sender)) return;
        RpcEndDrag(anchoredPos, parentPath);
    }

    // --- RPCs ---
    [ClientRpc]
    void RpcBeginDrag()
    {
        // Owner is already handling visuals locally via DraggableCoin; skip for owner.
        if (IsLocalOwner()) return;

        if (coinCanvasGroup) coinCanvasGroup.blocksRaycasts = false;
        if (coinRT && dragLayer) coinRT.SetParent(dragLayer, worldPositionStays: true);
    }

    [ClientRpc]
    void RpcUpdatePos(Vector2 anchoredPos)
    {
        if (IsLocalOwner()) return; // local owner already drives it
        if (coinRT) coinRT.anchoredPosition = anchoredPos;
    }

    [ClientRpc]
    void RpcEndDrag(Vector2 anchoredPos, string parentPath)
    {
        if (IsLocalOwner()) return; // local owner already placed it

        if (coinRT)
        {
            var targetParent = DecodePath(parentPath);
            if (targetParent)
                coinRT.SetParent(targetParent, worldPositionStays: false);

            coinRT.anchoredPosition = anchoredPos;
        }
        if (coinCanvasGroup) coinCanvasGroup.blocksRaycasts = true;
    }

    // --- Helpers ---
    bool IsLocalOwner()
    {
        var lp = NetworkClient.localPlayer;
        return lp && ownerNetId != 0 && lp.netId == ownerNetId;
    }

    bool IsValidSender(NetworkConnectionToClient sender)
    {
        if (ownerNetId == 0 || sender == null || sender.identity == null) return false;
        return sender.identity.netId == ownerNetId;
    }

    string EncodePath(Transform t)
    {
        if (!t || !pathRoot) return string.Empty;
        var sb = new StringBuilder();
        var cur = t;
        while (cur && cur != pathRoot)
        {
            sb.Insert(0, "/" + cur.name);
            cur = cur.parent;
        }
        return sb.ToString(); // "/Board/Cells/Cell_12/Anchor"
    }

    Transform DecodePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !pathRoot) return null;
        // Use Transform.Find with a relative path
        return pathRoot.Find(path.TrimStart('/'));
    }

#if UNITY_EDITOR
    void Reset()
    {
        coinRT = GetComponent<RectTransform>();
        coinCanvasGroup = GetComponent<CanvasGroup>();
        // Try to guess a path root (nearest Canvas)
        var canvas = GetComponentInParent<Canvas>();
        if (canvas) pathRoot = canvas.transform;
    }
#endif
}
