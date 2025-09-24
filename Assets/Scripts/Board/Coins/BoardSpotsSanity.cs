using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class BoardSpotsSanity : MonoBehaviour
{
    [Header("Run on Start for quick checks")]
    public bool runOnStart = true;

    void Start() { if (runOnStart) Run(); }

    [ContextMenu("Run Sanity Checks")]
    public void Run()
    {
        var board = FindObjectOfType<BoardSpotsNet>(true);
        var spots = FindObjectsOfType<ValidDropSpot>(true).OrderBy(s => s.spotIndex).ToList();

        Debug.Log($"[SANITY] BoardSpotsNet present: {board != null}  | spots found: {spots.Count}");

        // Board object present?
        if (!board)
        {
            Debug.LogWarning("[SANITY] BoardSpotsNet missing in scene (no NetworkIdentity managing occupancy).");
            return;
        }

        // Network identity on board?
        var bni = board.GetComponent<NetworkIdentity>();
        Debug.Log($"[SANITY] Board has NetworkIdentity: {bni != null}  | isServer={NetworkServer.active}  isClient={NetworkClient.active}");

        // Unique indices / Collider presence
        var duplicateIdx = spots.GroupBy(s => s.spotIndex).Where(g => g.Key >= 0 && g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIdx.Count > 0) Debug.LogError("[SANITY] Duplicate spotIndex values: " + string.Join(",", duplicateIdx));

        int noIndex = spots.Count(s => s.spotIndex < 0);
        if (noIndex > 0) Debug.LogWarning($"[SANITY] {noIndex} spots have spotIndex < 0 (unassigned).");

        int noCollider = spots.Count(s => !s.TryGetComponent<Collider2D>(out _));
        if (noCollider > 0) Debug.LogError($"[SANITY] {noCollider} spots have NO Collider2D.");

        // Placement flags
        int disabledSpots = spots.Count(s => !s.enabledForPlacement);
        int occupiedSpots = spots.Count(s => s.isOccupied || s.occupant != null);
        Debug.Log($"[SANITY] enabledForPlacement=true: {spots.Count - disabledSpots}  | disabled: {disabledSpots}  | occupied(local): {occupiedSpots}");

        // Compare board occupancy dict (server-authoritative) if available
        if (board.occupancy != null && board.occupancy.Count > 0)
        {
            int dictOccupied = board.occupancy.Count(kv => kv.Value != 0);
            var mismatches = new List<int>();
            foreach (var s in spots)
            {
                uint net = board.occupancy.ContainsKey(s.spotIndex) ? board.occupancy[s.spotIndex] : 0u;
                bool localOcc = s.isOccupied || s.occupant != null;
                if ((net != 0) != localOcc) mismatches.Add(s.spotIndex);
            }
            Debug.Log($"[SANITY] board.occupancy occupied: {dictOccupied}  | local/board mismatch spots: {mismatches.Count}");
            if (mismatches.Count > 0) Debug.LogWarning("[SANITY] Mismatch indices: " + string.Join(",", mismatches.Take(50)) + (mismatches.Count > 50 ? " ..." : ""));
        }
        else
        {
            Debug.Log("[SANITY] board.occupancy is empty or uninitialized on this peer (ok on fresh start).");
        }

        // Layer mask / ray test at mouse (optional live probe)
        var cam = Camera.main;
        if (cam)
        {
            var mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            var inRange = Physics2D.OverlapCircleAll(mouseWorld, 2f);
            int hits = inRange?.Length ?? 0;
            Debug.Log($"[SANITY] Probe around mouse @ {mouseWorld}: Physics2D hits = {hits}");
        }

        Debug.Log("[SANITY] Completed.");
    }
}
