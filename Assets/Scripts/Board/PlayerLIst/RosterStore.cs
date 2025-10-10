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

    public static void SetCurrentClueGiverByIndex(int rosterIndex)
    {
        if (Instance == null) new GameObject("RosterStore").AddComponent<RosterStore>();
        if (rosterIndex < 0 || rosterIndex >= Instance.Names.Count) return;
        Debug.Log(rosterIndex);
        var name = Instance.Names[rosterIndex];
        for (int i = 0; i < Instance.Names.Count; i++)
        {
            Debug.Log(Instance.Names[i]);
        }
        SetCurrentClueGiver(name);
    }

}
