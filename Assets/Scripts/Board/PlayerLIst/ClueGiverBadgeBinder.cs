using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class ClueGiverBadgeBinder : MonoBehaviour
{
    [SerializeField] string ownerName;
    [Header("If left empty")]
    [SerializeField] TMP_Text nameLabel;

    [Header("Badge Style")]
    [SerializeField] string badgeText = "CLUE GIVER";
    [SerializeField] float fontSizeScale = 0.7f;
    [SerializeField] FontStyles fontStyle = FontStyles.Bold;
    [SerializeField] TextAlignmentOptions alignment = TextAlignmentOptions.Left;
    [SerializeField] Vector2 anchoredOffset = new Vector2(0f, -6f);

    TMP_Text _badge;
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

            _badge.font = (nameLabel as TextMeshProUGUI)?.font;
            _badge.fontSize = Mathf.Max(1f, nameLabel.fontSize * fontSizeScale);
            _badge.fontStyle = fontStyle;
            _badge.alignment = alignment;
            _badge.enableAutoSizing = false;
            _badge.raycastTarget = false;

            int nameIdx = nameLabel.transform.GetSiblingIndex();
            _badge.transform.SetSiblingIndex(Mathf.Min(nameIdx + 1, parent.childCount - 1));

            var rt = (RectTransform)_badge.transform;
            rt.anchorMin = ((RectTransform)nameLabel.transform).anchorMin;
            rt.anchorMax = ((RectTransform)nameLabel.transform).anchorMax;
            rt.pivot = ((RectTransform)nameLabel.transform).pivot;
            rt.anchoredPosition = ((RectTransform)nameLabel.transform).anchoredPosition + anchoredOffset;

            _badge.text = badgeText;
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

        if (nameLabel) _badge.color = nameLabel.color;
    }
}
