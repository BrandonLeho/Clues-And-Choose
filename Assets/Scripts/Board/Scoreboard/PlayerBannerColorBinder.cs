using UnityEngine;
using UnityEngine.UI;

public class PlayerBannerColorBinder : MonoBehaviour
{
    [SerializeField] private string ownerName;
    [SerializeField] private Image image;
    [SerializeField] private Color fallbackColor = Color.white;
    [SerializeField] private bool preserveExistingAlpha = true;

    void Reset()
    {
        if (!image) image = GetComponentInChildren<Image>();
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
        Color c;
        if (!string.IsNullOrWhiteSpace(ownerName) &&
            RegistryNameColorLookup.TryGetColorForName(ownerName, out c))
        {
            Apply(c);
        }
        else
        {
            Apply(fallbackColor);
        }
    }

    void Apply(Color c)
    {
        if (!image) return;
        var x = c;
        if (preserveExistingAlpha) x.a = image.color.a;
        image.color = x;
    }
}