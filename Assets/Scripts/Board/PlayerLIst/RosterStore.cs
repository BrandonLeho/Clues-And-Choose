using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class RosterStore : MonoBehaviour
{
    public static RosterStore Instance;
    public List<string> Names = new List<string>();

    public static event System.Action<string> OnClueGiverChanged;
    public static string CurrentClueGiverName { get; private set; } = null;
    public static string LocalPlayerName { get; private set; } = null;

    public static event System.Action<uint> OnClueGiverIdChanged;
    public static uint CurrentClueGiverId { get; private set; } = 0;

    public static System.Func<uint, string> ResolveNameFromId;

    void OnEnable()
    {
        TryBindRoundManager();
    }

    System.Collections.IEnumerator Start()
    {
        yield return null;
        TryBindRoundManager();
    }

    void TryBindRoundManager()
    {
        var rm = RoundManager.Instance ?? FindFirstObjectByType<RoundManager>();
        if (rm == null) return;

        rm.onClueGiverChangedClient.RemoveListener(OnClueGiverChangedFromRoundManager);
        rm.onClueGiverChangedClient.AddListener(OnClueGiverChangedFromRoundManager);
    }

    void OnClueGiverChangedFromRoundManager(uint newNetId)
    {
        SetCurrentClueGiverById(newNetId);
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void SaveNames(IReadOnlyList<string> names)
    {
        if (Instance == null) new GameObject("RosterStore").AddComponent<RosterStore>();
        Instance.Names = new List<string>(names);
    }


    public static void SetLocalPlayerName(string localName)
    {
        if (Instance == null) new GameObject("RosterStore").AddComponent<RosterStore>();
        LocalPlayerName = localName;
    }

    public static void SetCurrentClueGiver(string name)
    {
        Debug.Log("Roster Clue Giver: " + name);
        if (Instance == null) new GameObject("RosterStore").AddComponent<RosterStore>();
        CurrentClueGiverName = name;
        OnClueGiverChanged?.Invoke(name);
    }

    public static void SetCurrentClueGiverById(uint netId)
    {
        if (Instance == null) new GameObject("RosterStore").AddComponent<RosterStore>();

        CurrentClueGiverId = netId;
        OnClueGiverIdChanged?.Invoke(netId);

        if (ResolveNameFromId != null)
        {
            var name = ResolveNameFromId(netId);
            if (!string.IsNullOrEmpty(name))
            {
                CurrentClueGiverName = name;
                OnClueGiverChanged?.Invoke(name);
            }
        }
    }
}
