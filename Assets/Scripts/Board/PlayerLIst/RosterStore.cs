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

    public static void SaveNames(IReadOnlyList<string> names)
    {
        EnsureInstance();
        Instance.Names = names != null ? new List<string>(names) : new List<string>();
    }

    public static void SaveRoster(IReadOnlyList<uint> netIds, IReadOnlyList<string> names)
    {
        EnsureInstance();
        _idToName.Clear();

        if (names != null) Instance.Names = new List<string>(names);
        if (netIds == null || names == null) return;

        int count = Mathf.Min(netIds.Count, names.Count);
        for (int i = 0; i < count; i++)
            _idToName[netIds[i]] = names[i];
    }

    public static void SaveOrUpdateName(uint netId, string name)
    {
        EnsureInstance();
        _idToName[netId] = name;
        if (!Instance.Names.Contains(name))
            Instance.Names.Add(name);
    }

    static bool TryResolveIdentity(uint netId, out NetworkIdentity identity)
    {
        identity = null;

        if (NetworkClient.active && NetworkClient.spawned != null &&
            NetworkClient.spawned.TryGetValue(netId, out identity) && identity)
            return true;

        if (NetworkServer.active && NetworkServer.spawned != null &&
            NetworkServer.spawned.TryGetValue(netId, out identity) && identity)
            return true;

        return false;
    }

    public static bool TryGetNameByNetId(uint netId, out string name)
    {
        if (_idToName.TryGetValue(netId, out name))
            return true;

        if (TryResolveIdentity(netId, out var identity) && identity && identity.gameObject)
        {
            name = identity.gameObject.name;
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
        Debug.Log("Roster Clue Giver: " + name);
        EnsureInstance();
        CurrentClueGiverName = name;
        OnClueGiverChanged?.Invoke(name);
    }

    public static void SetCurrentClueGiverByNetId(uint netId)
    {
        EnsureInstance();

        if (netId == 0)
        {
            SetCurrentClueGiver(null);
            return;
        }

        if (!TryGetNameByNetId(netId, out var name))
            name = $"NetId:{netId}";

        SetCurrentClueGiver(name);
    }
}
