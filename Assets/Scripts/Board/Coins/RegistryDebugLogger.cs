using UnityEngine;

public class RegistryDebugLogger : MonoBehaviour
{
    [ContextMenu("Log Registry State")]
    public void LogRegistry()
    {
        var reg = ColorLockRegistry.GetOrFind();
        if (!reg)
        {
            Debug.LogWarning("[RegistryDebugLogger] No registry instance found.");
            return;
        }

        Debug.Log("=== ColorLockRegistry State ===");
        foreach (var kv in reg.lockedBy)
            Debug.Log($"Index {kv.Key} → Owner NetID {kv.Value}");

        foreach (var kv in reg.colorByOwner)
            Debug.Log($"Owner {kv.Key} → Color {kv.Value}");

        foreach (var kv in reg.indexByOwner)
            Debug.Log($"Owner {kv.Key} → Locked Index {kv.Value}");

        foreach (var kv in reg.labelByIndex)
            Debug.Log($"Index {kv.Key} → Label \"{kv.Value}\"");
        Debug.Log("==============================");
    }
}
