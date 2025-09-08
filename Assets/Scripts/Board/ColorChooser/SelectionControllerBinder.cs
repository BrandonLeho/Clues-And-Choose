using System.Collections;
using Mirror;
using UnityEngine;

public class ColorPickerUIBinder : MonoBehaviour
{
    [Tooltip("Leave empty to auto-find in children")]
    public SelectionController picker;

    void Awake()
    {
        if (!picker)
            picker = GetComponentInChildren<SelectionController>(true);
    }

    [ClientCallback]
    IEnumerator Start()
    {
        // Wait until client + local player exists
        yield return new WaitUntil(() => NetworkClient.active && NetworkClient.localPlayer != null);

        var chooser = NetworkClient.localPlayer.GetComponent<PlayerColorChooser>();
        if (!chooser)
        {
            Debug.LogWarning("ColorPickerUIBinder: local PlayerColorChooser not found.");
            yield break;
        }

        chooser.SetPicker(picker);   // hands the UI to the local player script
    }
}
