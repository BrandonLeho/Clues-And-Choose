using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class ClueGiverBadgeBinder : MonoBehaviour
{
    [SerializeField] string ownerName;
    [Header("If left empty")]
    [SerializeField] TMP_Text nameLabel;

    [Header("Badge Style")]
    [SerializeField] string badgeText = "CLUE GIVER";
    [SerializeField, Min(0.1f)] float fontSizeScale = 0.7f;
    [SerializeField] FontStyles fontStyle = FontStyles.Bold;
    [SerializeField] TextAlignmentOptions alignment = TextAlignmentOptions.Left;
    [SerializeField] Vector2 anchoredOffset = new Vector2(0f, -6f);

    TMP_Text _badge;
    Outline _nameOutlineUI;
    Outline _badgeOutlineUI;

    bool _initialized;

    void Reset()
    {
        if (!nameLabel) nameLabel = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        TryInit();
        RosterStore.OnClueGiverChanged += HandleClueGiverChanged;
        Refresh();
    }

    void OnDisable()
    {
        RosterStore.OnClueGiverChanged -= HandleClueGiverChanged;
    }

    public void SetOwnerName(string name)
    {
        ownerName = name;
        Refresh();
    }

    void HandleClueGiverChanged(string _)
    {
        Refresh();
    }

    void TryInit()
    {
        if (_initialized) return;

        if (!nameLabel) nameLabel = GetComponentInChildren<TMP_Text>(true);
        if (!nameLabel) return;

        if (!_badge)
        {
            var parent = nameLabel.transform.parent != null ? nameLabel.transform.parent : transform;
            var go = new GameObject("ClueGiverLabel", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            _badge = go.AddComponent<TextMeshProUGUI>();

            var nameTMP = nameLabel as TextMeshProUGUI;
            _badge.font = nameTMP ? nameTMP.font : _badge.font;
            _badge.fontSize = Mathf.Max(1f, nameLabel.fontSize * fontSizeScale);
            _badge.fontStyle = fontStyle;
            _badge.alignment = alignment;
            _badge.enableAutoSizing = false;
            _badge.raycastTarget = false;
            _badge.text = badgeText;

            int nameIdx = nameLabel.transform.GetSiblingIndex();
            _badge.transform.SetSiblingIndex(Mathf.Min(nameIdx + 1, parent.childCount - 1));

            var nrt = (RectTransform)nameLabel.transform;
            var brt = (RectTransform)_badge.transform;
            brt.anchorMin = nrt.anchorMin;
            brt.anchorMax = nrt.anchorMax;
            brt.pivot = nrt.pivot;
            brt.anchoredPosition = nrt.anchoredPosition + anchoredOffset;

            _nameOutlineUI = nameLabel.GetComponent<Outline>();
            _badgeOutlineUI = _badge.GetComponent<Outline>();
            if (_nameOutlineUI && !_badgeOutlineUI) _badgeOutlineUI = _badge.gameObject.AddComponent<Outline>();
        }

        _initialized = true;
    }

    void Refresh()
    {
        TryInit();
        if (!_badge) return;

        bool isClueGiver = !string.IsNullOrWhiteSpace(ownerName) &&
                           string.Equals(ownerName, RosterStore.CurrentClueGiverName, System.StringComparison.Ordinal);

        _badge.gameObject.SetActive(isClueGiver);

        _badge.color = nameLabel ? nameLabel.color : _badge.color;

        SyncOutlineFromNameToBadge();
    }

    void SyncOutlineFromNameToBadge()
    {
        var nameTMP = nameLabel as TextMeshProUGUI;
        var badgeTMP = _badge as TextMeshProUGUI;

        if (nameTMP && badgeTMP)
        {
            var src = nameTMP.fontMaterial;
            if (src != null)
            {
                var dst = badgeTMP.fontMaterial;
                if (dst == null || dst.shader != src.shader)
                {
                    dst = new Material(src);
                    badgeTMP.fontSharedMaterial = dst;
                }

                if (src.HasProperty("_OutlineColor") && dst.HasProperty("_OutlineColor"))
                {
                    var oc = src.GetColor("_OutlineColor");
                    dst.SetColor("_OutlineColor", oc);
                }
                if (src.HasProperty("_OutlineWidth") && dst.HasProperty("_OutlineWidth"))
                {
                    var ow = src.GetFloat("_OutlineWidth");
                    dst.SetFloat("_OutlineWidth", ow);
                }
            }
        }

        if (_nameOutlineUI)
        {
            if (!_badgeOutlineUI) _badgeOutlineUI = _badge.gameObject.AddComponent<Outline>();
            _badgeOutlineUI.effectColor = _nameOutlineUI.effectColor;
            _badgeOutlineUI.effectDistance = _nameOutlineUI.effectDistance;
            _badgeOutlineUI.useGraphicAlpha = _nameOutlineUI.useGraphicAlpha;
        }
        else
        {
            if (_badgeOutlineUI) _badgeOutlineUI.enabled = false;
        }
    }
}
