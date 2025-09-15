using System.Collections.Generic;
using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    [Header("UI Grid (Spawner Source)")]
    [SerializeField] RectTransform gridParent;
    [SerializeField] Canvas sourceCanvas;

    [Header("World (Spawn Target)")]
    [SerializeField] Camera worldCamera;
    [SerializeField] Transform worldParent;
    [SerializeField] GameObject coinPrefab;
    [SerializeField] float spawnZ = 0f;

    [Header("Sizing")]
    [Tooltip("If true, scales coin to fit the slot size in world units.")]
    [SerializeField] bool fitScaleToSlot = true;
    [Tooltip("0.0 = coin matches slot exactly; 0.85 = leave padding so rim isn’t clipped.")]
    [Range(0.5f, 1.0f)][SerializeField] float slotFillRatio = 0.85f;

    [Header("Drag Integration (optional)")]
    [SerializeField] bool configureDragOnSpawn = true;

    List<RectTransform> _slots = new();

    void Reset()
    {
        sourceCanvas = GetComponentInParent<Canvas>();
        if (!worldCamera) worldCamera = Camera.main;
        if (!worldParent) worldParent = new GameObject("Coins_WorldRoot").transform;
    }

    void Awake()
    {
        if (!worldCamera) worldCamera = Camera.main;
    }

    public void CollectSlots()
    {
        _slots.Clear();
        if (!gridParent) return;
        for (int i = 0; i < gridParent.childCount; i++)
        {
            if (gridParent.GetChild(i) is RectTransform rt) _slots.Add(rt);
        }
    }

    public void SpawnForPlayers(int playerCount, IList<Color> playerColors = null)
    {
        if (!coinPrefab || !sourceCanvas || !gridParent)
        {
            Debug.LogError("[CoinSpawnerUIToWorld] Missing references.");
            return;
        }

        if (_slots.Count == 0) CollectSlots();

        int needed = playerCount * 2;
        if (needed > _slots.Count)
        {
            Debug.LogWarning($"[CoinSpawnerUIToWorld] Not enough slots. Needed {needed}, have {_slots.Count}. Spawning only {_slots.Count}.");
            needed = _slots.Count;
        }

        for (int p = 0; p < playerCount; p++)
        {
            for (int c = 0; c < 2; c++)
            {
                int slotIndex = p * 2 + c;
                if (slotIndex >= needed) break;

                var slot = _slots[slotIndex];
                Vector3 worldPos = SlotCenterOnWorldPlane(slot, spawnZ);

                var coin = Instantiate(coinPrefab, worldPos, Quaternion.identity, worldParent);

                if (fitScaleToSlot) FitCoinScaleToSlot(coin, slot, spawnZ);

                if (playerColors != null && p < playerColors.Count)
                {
                    var vis = coin.GetComponent<CoinVisual>();
                    if (vis) vis.SetBaseColor(playerColors[p]);
                }

                if (configureDragOnSpawn)
                {
                    var drag = coin.GetComponent<CoinDragHandler>();
                    if (drag)
                    {
                        drag.worldCamera = worldCamera;
                        drag.dragZ = spawnZ;
                    }
                }
            }
        }
    }

    Vector3 SlotCenterOnWorldPlane(RectTransform slot, float planeZ)
    {
        Vector3 uiWorldCenter = slot.TransformPoint(slot.rect.center);

        Camera canvasCam = null;
        if (sourceCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
            sourceCanvas.renderMode == RenderMode.WorldSpace)
        {
            canvasCam = sourceCanvas.worldCamera;
        }

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(canvasCam, uiWorldCenter);

        if (!worldCamera) worldCamera = Camera.main;
        var ray = worldCamera.ScreenPointToRay(screenPos);


        var plane = new Plane(-worldCamera.transform.forward, new Vector3(0, 0, planeZ));
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }


        var wp = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, planeZ));
        wp.z = planeZ;
        return wp;
    }

    void FitCoinScaleToSlot(GameObject coin, RectTransform slot, float planeZ)
    {
        var sr = coin.GetComponentInChildren<SpriteRenderer>();
        if (!sr || sr.sprite == null) return;

        Vector3[] corners = new Vector3[4];
        slot.GetWorldCorners(corners);

        Camera canvasCam = null;
        if (sourceCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
            sourceCanvas.renderMode == RenderMode.WorldSpace)
        {
            canvasCam = sourceCanvas.worldCamera;
        }

        Vector2 tl = RectTransformUtility.WorldToScreenPoint(canvasCam, corners[1]); // top-left
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(canvasCam, corners[2]); // top-right
        Vector2 bl = RectTransformUtility.WorldToScreenPoint(canvasCam, corners[0]); // bottom-left

        Vector3 w_tl = ScreenToWorldOnPlane(tl, planeZ);
        Vector3 w_tr = ScreenToWorldOnPlane(tr, planeZ);
        Vector3 w_bl = ScreenToWorldOnPlane(bl, planeZ);

        float slotWidthWorld = Vector3.Distance(w_tl, w_tr);
        float slotHeightWorld = Vector3.Distance(w_bl, w_tl);
        float targetDiameter = Mathf.Min(slotWidthWorld, slotHeightWorld) * slotFillRatio;

        var originalScale = coin.transform.localScale;

        coin.transform.localScale = Vector3.one;
        float coinWidthWorld = sr.bounds.size.x;
        if (coinWidthWorld <= 0f) coinWidthWorld = 1f;
        float scaleFactor = targetDiameter / coinWidthWorld;
        coin.transform.localScale = originalScale * scaleFactor;
    }

    Vector3 ScreenToWorldOnPlane(Vector2 screenPos, float planeZ)
    {
        if (!worldCamera) worldCamera = Camera.main;
        var ray = worldCamera.ScreenPointToRay(screenPos);
        var plane = new Plane(-worldCamera.transform.forward, new Vector3(0, 0, planeZ));
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter)
                                                   : worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, planeZ));
    }
}
