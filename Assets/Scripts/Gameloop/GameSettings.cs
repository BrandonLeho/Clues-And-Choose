using UnityEngine;

[CreateAssetMenu(menuName = "HuesAndCues/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Win / Scoring")]
    [Min(1)] public int targetScore = 50;
    [Min(1)] public int maxRounds = 20;

    [Header("Round Flow")]
    public float phaseDuration_Clue = 25f;
    public float phaseDuration_Guess = 45f;
    public float phaseDuration_Scoring = 6f;

    [Header("Turn Order")]
    public bool rotateClueGiverEveryRound = true;
    public bool allowJoinMidGame = true;
}
