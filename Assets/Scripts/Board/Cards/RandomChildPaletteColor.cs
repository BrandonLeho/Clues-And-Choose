using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class RandomChildPaletteColor : MonoBehaviour
{
    [Header("Source Palette (ScriptableObject)")]
    [SerializeField] private PaletteGrid palette;

    [Header("Search")]
    [SerializeField] private Transform root;

    [Header("Label")]
    [SerializeField] private string labelFormat = "{0}{1}";
    [SerializeField] private bool padColumnsTo2 = false;

    [Header("Distinctness")]
    [Range(0f, 1.732f)] public float minLinearRgbDistance = 0.65f;
    public int maxAttemptsPerPick = 200;
    public float relaxStep = 0.1f;

    void Reset() { root = transform; }

    void OnEnable() => ApplyRandomColorsAndLabels();

    [ContextMenu("Apply Random Colors + Labels Now")]
    public void ApplyRandomColorsAndLabels()
    {
        if (palette == null) { Debug.LogWarning("[RandomChildPaletteColor] No PaletteGrid assigned."); return; }
        if (root == null) root = transform;

        var targets = new List<(Image img, TMP_Text label, Transform t)>();
        foreach (Transform child in root)
        {
            var img = child.GetComponentInChildren<Image>(true);
            if (img == null) continue;
            var label = child.GetComponentInChildren<TMP_Text>(true);
            targets.Add((img, label, child));
        }
        if (targets.Count == 0) return;

        int cols = Mathf.Max(1, palette.cols);
        int rows = Mathf.Max(1, palette.rows);
        int total = cols * rows;

        var picks = PickDistinctCells(targets.Count, cols, rows, total);

        for (int i = 0; i < targets.Count; i++)
        {
            var (x, y) = picks[i];
            Color c = palette.ColorAt(x, y);
            targets[i].img.color = c;

            var coord = targets[i].t.GetComponent<ChoiceGridCoord>();
            if (!coord) coord = targets[i].t.gameObject.AddComponent<ChoiceGridCoord>();
            coord.col = x;
            coord.row = y;

            char rowLetter = GetRowLetterBottomToTop(y, rows, palette.yZeroAtTop);
            int columnNumber = x + 1;

            if (targets[i].label != null)
            {
                string colStr = padColumnsTo2 ? columnNumber.ToString("00") : columnNumber.ToString();
                targets[i].label.text = string.Format(labelFormat, rowLetter, colStr);
            }
        }
    }

    private static char GetRowLetterBottomToTop(int y, int totalRows, bool yZeroAtTop)
    {
        int rowFromBottom = yZeroAtTop ? (totalRows - 1 - y) : y;
        rowFromBottom = Mathf.Clamp(rowFromBottom, 0, 25);
        return (char)('A' + rowFromBottom);
    }

    private List<(int x, int y)> PickDistinctCells(int needed, int cols, int rows, int maxCells)
    {
        var chosen = new List<(int x, int y)>(needed);
        var chosenColors = new List<Vector3>(needed);

        float currentMin = Mathf.Clamp(minLinearRgbDistance, 0f, 1.732f);
        int safetyRelaxations = 0;

        while (chosen.Count < needed)
        {
            bool placed = false;
            int attempts = 0;

            while (attempts++ < maxAttemptsPerPick)
            {
                int pick = Random.Range(0, maxCells);
                int x = pick % cols;
                int y = pick / cols;

                Color srgb = palette.ColorAt(x, y);
                Vector3 lin = ToLinearVec3(srgb);

                if (IsFarFromAll(lin, chosenColors, currentMin))
                {
                    chosen.Add((x, y));
                    chosenColors.Add(lin);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                safetyRelaxations++;
                currentMin = Mathf.Max(0f, currentMin - relaxStep);

                if (safetyRelaxations > 8)
                {
                    while (chosen.Count < needed)
                    {
                        int pick = Random.Range(0, maxCells);
                        chosen.Add((pick % cols, pick / cols));
                    }
                    break;
                }
            }
        }

        return chosen;
    }

    private static bool IsFarFromAll(Vector3 c, List<Vector3> bag, float minDist)
    {
        for (int i = 0; i < bag.Count; i++)
        {
            if (Vector3.Distance(c, bag[i]) < minDist)
                return false;
        }
        return true;
    }

    private static Vector3 ToLinearVec3(Color srgb)
    {
        Color lin = srgb.linear;
        return new Vector3(lin.r, lin.g, lin.b);
    }
}
