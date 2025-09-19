using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class CoinNetworkSpawner : NetworkBehaviour
{
    [Header("UI Grid (slots)")]
    [SerializeField] RectTransform gridParent;
    [SerializeField] Canvas sourceCanvas;
    [Header("World")]
    [SerializeField] Camera worldCamera;
    [SerializeField] Transform worldParent;
    [SerializeField] GameObject coinPrefab;
    [SerializeField] float spawnZ = 0f;
    [Header("Sizing")]
    [SerializeField] bool fitScaleToSlot = true;
    [Range(0.5f, 1f)] public float slotFillRatio = 0.85f;

    bool _spawned;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!worldCamera) worldCamera = Camera.main;
    }

    [Server]
    public void ServerSpawnFromPhaseEnd()
    {
        if (_spawned) return;
        TrySpawnOnce();
    }

    [Server]
    public void TrySpawnOnce()
    {
        if (_spawned) return;
        if (!gridParent || !coinPrefab) return;

        if (!worldCamera) worldCamera = Camera.main;
        bool usingFallback = worldCamera == null;

        var reg = ColorLockRegistry.GetOrFind();
        if (!reg) return;

        int players = NetworkServer.spawned.Values.Count(id => id && id.GetComponent<PlayerNameSync>());
        int chosen = reg.colorByOwner.Count;
        if (players <= 0 || chosen < players) return;

        var ordered = reg.lockedBy.OrderBy(kv => kv.Key).ToList();

        int coinsNeeded = Mathf.Min(ordered.Count * 2, gridParent.childCount);
        if (coinsNeeded == 0) return;

        var slots = new List<RectTransform>(gridParent.childCount);
        for (int i = 0; i < gridParent.childCount; i++)
        {
            var ch = gridParent.GetChild(i);
            var rt = ch as RectTransform;
            if (rt) slots.Add(rt);
        }
        if (slots.Count == 0) return;

        Vector3 fbStart = worldParent ? worldParent.position : Vector3.zero;
        float fbStep = 1.0f;

        for (int p = 0; p < ordered.Count; p++)
        {
            uint ownerId = ordered[p].Value;
            Color32 color = reg.colorByOwner.TryGetValue(ownerId, out var c32) ? c32 : (Color32)Color.gray;

            for (int k = 0; k < 2; k++)
            {
                int slotIdx = p * 2 + k;
                if (slotIdx >= coinsNeeded) break;

                try
                {
                    RectTransform slot = (slotIdx < slots.Count) ? slots[slotIdx] : null;
                    if (!slot) continue;

                    Vector3 pos = usingFallback
                        ? fbStart + new Vector3(slotIdx * fbStep, 0f, spawnZ)
                        : SlotCenterOnWorldPlane(slot, spawnZ);

                    var go = Instantiate(coinPrefab, pos, Quaternion.identity, worldParent);
                    var netCoin = go.GetComponent<NetworkCoin>();
                    if (netCoin)
                    {
                        netCoin.ownerNetId = ownerId;
                        netCoin.netColor = color;
                    }

                    if (fitScaleToSlot && !usingFallback)
                        FitCoinScaleToSlot(go, slot, spawnZ);

                    NetworkServer.Spawn(go);
                }
                catch { /* silently ignore exceptions */ }
            }
        }

        _spawned = true;
    }

    Vector3 SlotCenterOnWorldPlane(RectTransform slot, float planeZ)
    {
        Vector3 uiWorldCenter = slot.TransformPoint(slot.rect.center);
        Camera canvasCam = null;
        if (sourceCanvas && (sourceCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
                             sourceCanvas.renderMode == RenderMode.WorldSpace))
            canvasCam = sourceCanvas.worldCamera;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(canvasCam, uiWorldCenter);

        if (!worldCamera) worldCamera = Camera.main;
        var ray = worldCamera.ScreenPointToRay(screenPos);
        var plane = new Plane(-worldCamera.transform.forward, new Vector3(0, 0, planeZ));
        if (plane.Raycast(ray, out float enter)) return ray.GetPoint(enter);

        var wp = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, planeZ));
        wp.z = planeZ;
        return wp;
    }

    void FitCoinScaleToSlot(GameObject coin, RectTransform slot, float planeZ)
    {
        if (!fitScaleToSlot) return;
        var sr = coin.GetComponentInChildren<SpriteRenderer>();
        if (!sr || !sr.sprite) return;

        Vector3[] c = new Vector3[4];
        slot.GetWorldCorners(c);
        Camera canvasCam = (sourceCanvas && (sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay))
            ? sourceCanvas.worldCamera
            : null;

        Vector2 tl = RectTransformUtility.WorldToScreenPoint(canvasCam, c[1]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(canvasCam, c[2]);
        Vector2 bl = RectTransformUtility.WorldToScreenPoint(canvasCam, c[0]);

        Vector3 w_tl = ScreenToWorldOnPlane(tl, planeZ);
        Vector3 w_tr = ScreenToWorldOnPlane(tr, planeZ);
        Vector3 w_bl = ScreenToWorldOnPlane(bl, planeZ);

        float slotW = Vector3.Distance(w_tl, w_tr);
        float slotH = Vector3.Distance(w_bl, w_tl);
        float targetDia = Mathf.Min(slotW, slotH) * slotFillRatio;

        var original = coin.transform.localScale;
        coin.transform.localScale = Vector3.one;
        float coinW = sr.bounds.size.x;
        if (coinW <= 0f) coinW = 1f;
        coin.transform.localScale = original * (targetDia / coinW);
    }

    Vector3 ScreenToWorldOnPlane(Vector2 sp, float planeZ)
    {
        if (!worldCamera) worldCamera = Camera.main;
        var ray = worldCamera.ScreenPointToRay(sp);
        var plane = new Plane(-worldCamera.transform.forward, new Vector3(0, 0, planeZ));
        return plane.Raycast(ray, out float enter)
            ? ray.GetPoint(enter)
            : worldCamera.ScreenToWorldPoint(new Vector3(sp.x, sp.y, planeZ));
    }
}
