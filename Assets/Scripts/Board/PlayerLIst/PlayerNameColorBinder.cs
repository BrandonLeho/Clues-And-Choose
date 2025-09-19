using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerNameColorBinder : MonoBehaviour
{
    [SerializeField] string ownerName;
    [SerializeField] TextMeshProUGUI tmp;
    [SerializeField] Text uiText;
    [SerializeField] Color fallbackColor = Color.white;
    [SerializeField] bool preserveExistingAlpha = true;

    void Reset()
    {
        tmp = GetComponentInChildren<TextMeshProUGUI>();
        if (!tmp) uiText = GetComponentInChildren<Text>();
    }

    void OnEnable()
    {
        var reg = ColorLockRegistry.GetOrFind();
        if (reg != null) reg.OnRegistryChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        var reg = ColorLockRegistry.GetOrFind();
        if (reg != null) reg.OnRegistryChanged -= Refresh;
    }

    public void SetOwnerName(string name)
    {
        ownerName = name;
        Refresh();
    }

    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(ownerName)) { ApplyColor(fallbackColor); return; }

        if (RegistryNameColorLookup.TryGetColorForName(ownerName, out var c))
            ApplyColor(c);
        else
            ApplyColor(fallbackColor);
    }

    void ApplyColor(Color c)
    {
        if (tmp)
        {
            var x = c; if (preserveExistingAlpha) x.a = tmp.color.a;
            tmp.color = x;
        }
        if (uiText)
        {
            var x = c; if (preserveExistingAlpha) x.a = uiText.color.a;
            uiText.color = x;
        }
    }
}
