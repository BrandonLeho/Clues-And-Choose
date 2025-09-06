using UnityEngine;
using TMPro;

public class PlayerRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;

    public void SetName(string name)
    {
        nameText.text = name;
    }

    public void SetColor(Color color)
    {
        nameText.color = color;
    }

    public void SetReady(bool isReady)
    {
        // Example: append a "Ready!" tag
        nameText.text = isReady ? $"{nameText.text} (Ready)" : nameText.text.Replace(" (Ready)", "");
    }
}
