using UnityEngine;

public static class RegistryNameColorLookup
{
    public static bool TryGetColorForName(string ownerName, out Color color)
    {
        color = Color.white;
        var reg = ColorLockRegistry.GetOrFind();
        if (reg == null || string.IsNullOrWhiteSpace(ownerName)) return false;

        foreach (var kv in reg.lockedBy)
        {
            int index = kv.Key;
            uint netId = kv.Value;

            if (reg.labelByIndex.TryGetValue(index, out var label) &&
                string.Equals(label, ownerName, System.StringComparison.Ordinal))
            {
                if (reg.colorByOwner.TryGetValue(netId, out var c32))
                {
                    color = c32;
                    return true;
                }
            }
        }
        return false;
    }
}
