using UnityEngine;

public class HotkeyRebuilder : MonoBehaviour
{
    public ScoreboardRowAutoSizer autosizer;

    public KeyCode hotkey = KeyCode.R;

    void Update()
    {
        if (Input.GetKeyDown(hotkey))
        {
            if (autosizer != null)
            {
                autosizer.ResizeNow(null);
            }
        }
    }
}
