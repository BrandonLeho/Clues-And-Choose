using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class RosterStore : MonoBehaviour
{
    public static RosterStore Instance;
    public List<string> Names = new List<string>();

    public static event System.Action<string> OnClueGiverChanged;
    public static string CurrentClueGiverName { get; private set; } = null;
    public static string LocalPlayerName { get; private set; } = null;

    static readonly Dictionary<uint, string> _idToName = new Dictionary<uint, string>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    static void EnsureInstance()
    {
        if (Instance == null) new GameObject("RosterStore").AddComponent<RosterStore>();
    }

    public static void SaveRoster(IReadOnlyList<uint> netIds, IReadOnlyList<string> names)
    {
        EnsureInstance();
        _idToName.Clear();
        Instance.Names.Clear();

        if (netIds == null || names == null)
            return;

        int count = Mathf.Min(netIds.Count, names.Count);
        for (int i = 0; i < count; i++)
        {
            uint id = netIds[i];
            string name = names[i];
            _idToName[id] = name;
            Instance.Names.Add(name);
        }
    }

    public static void SaveOrUpdateName(uint netId, string name)
    {
        EnsureInstance();
        if (string.IsNullOrWhiteSpace(name)) return;
        _idToName[netId] = name;
        if (!Instance.Names.Contains(name)) Instance.Names.Add(name);
    }

    static bool TryResolveIdentity(uint netId, out NetworkIdentity id)
    {
        id = null;
        if (NetworkClient.active && NetworkClient.spawned != null &&
            NetworkClient.spawned.TryGetValue(netId, out id) && id) return true;

        if (NetworkServer.active && NetworkServer.spawned != null &&
            NetworkServer.spawned.TryGetValue(netId, out id) && id) return true;

        return false;
    }

    static bool TryGetDisplayNameFromIdentity(NetworkIdentity id, out string name)
    {
        name = null;
        if (!id) return false;

        var pns = id.GetComponent<PlayerNameSync>()
               ?? id.GetComponentInChildren<PlayerNameSync>(true)
               ?? id.GetComponentInParent<PlayerNameSync>(true);

        if (pns != null && !string.IsNullOrWhiteSpace(pns.DisplayName))
        {
            name = pns.DisplayName.Trim();
            return true;
        }
        return false;
    }

    public static bool TryGetNameByNetId(uint netId, out string name)
    {
        if (_idToName.TryGetValue(netId, out name))
            return true;

        if (TryResolveIdentity(netId, out var id))
        {
            if (TryGetDisplayNameFromIdentity(id, out name))
            {
                _idToName[netId] = name;
                if (!Instance.Names.Contains(name)) Instance.Names.Add(name);
                return true;
            }

            name = id.gameObject.name;
            return true;
        }

        name = $"NetId:{netId}";
        return false;
    }

    public static void SetLocalPlayerName(string localName)
    {
        EnsureInstance();
        LocalPlayerName = localName;
    }

    public static void SetCurrentClueGiver(string name)
    {
        EnsureInstance();
        CurrentClueGiverName = name;
        OnClueGiverChanged?.Invoke(name);
    }

    public static void SetCurrentClueGiverByNetId(uint netId)
    {
        EnsureInstance();
        if (netId == 0) { SetCurrentClueGiver(null); return; }
        if (!TryGetNameByNetId(netId, out var name)) name = $"NetId:{netId}";
        SetCurrentClueGiver(name);
    }
}
