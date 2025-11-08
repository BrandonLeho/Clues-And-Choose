using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerNameOutlineBinder : MonoBehaviour
{
    [SerializeField] string ownerName;
    [Header("Targets")]
    [SerializeField] TextMeshProUGUI tmp;
    [SerializeField] Text uiText;

    [Header("Outline Tuning")]
    [SerializeField] float tmpOutlineWidth = 0.2f;
    [SerializeField] Color fallbackColor = Color.white;

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
        Color c;
        if (!string.IsNullOrWhiteSpace(ownerName) &&
            RegistryNameColorLookup.TryGetColorForName(ownerName, out c))
        {
            ApplyOutline(c);
        }
        else
        {
            ApplyOutline(fallbackColor);
        }
    }

    void ApplyOutline(Color c)
    {
        if (tmp)
        {
            var mat = tmp.fontMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, c);
                if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, tmpOutlineWidth);

                tmp.fontMaterial = mat;
                tmp.SetMaterialDirty();
            }
        }

        if (uiText)
        {
            var outline = uiText.GetComponent<Outline>();
            if (!outline) outline = uiText.gameObject.AddComponent<Outline>();
            outline.effectColor = c;
            if (outline.effectDistance == Vector2.zero)
                outline.effectDistance = new Vector2(1f, -1f);
        }
    }
}
