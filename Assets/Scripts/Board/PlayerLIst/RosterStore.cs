// RosterStore.cs  (put in any folder)
using System.Collections.Generic;
using UnityEngine;

public class RosterStore : MonoBehaviour
{
    public static RosterStore Instance;
    public List<string> Names = new List<string>();

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
}
