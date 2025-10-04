using UnityEngine;
using TMPro;

public class ScoreboardEntryRefs : MonoBehaviour
{
    public TMP_Text nameTMP;
    public TMP_Text scoreTMP;

    void Reset()
    {
        var tmps = GetComponentsInChildren<TMP_Text>(true);
        if (tmps != null && tmps.Length > 0) nameTMP = tmps[0];
        if (tmps != null && tmps.Length > 1) scoreTMP = tmps[1];
    }
}
