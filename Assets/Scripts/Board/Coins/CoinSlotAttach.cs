// CoinSlotAttach.cs
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class CoinSlotAttach : NetworkBehaviour
{
    [SyncVar] public int slotIndex;
    [SyncVar] public string slotsRootTag = "CoinSlotsRoot";

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Find the slots root reliably
        Transform slotsRoot = null;
        var tagged = GameObject.FindWithTag(slotsRootTag);
        if (tagged) slotsRoot = tagged.transform;
        else
        {
            // Fallback: first GridLayoutGroup in scene
            var gl = GameObject.FindFirstObjectByType<GridLayoutGroup>();
            if (gl) slotsRoot = gl.transform;
        }

        if (slotsRoot == null || slotIndex < 0 || slotIndex >= slotsRoot.childCount)
            return;

        var slot = slotsRoot.GetChild(slotIndex);
        transform.SetParent(slot, false);

        if (transform is RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
