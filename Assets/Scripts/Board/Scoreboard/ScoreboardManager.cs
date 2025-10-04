using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreboardManager : MonoBehaviour
{
    [Header("Build")]
    [SerializeField] private Transform listParent;
    [SerializeField] private GameObject bannerPrefab;

    [Header("Name Rendering")]
    [SerializeField] private float maxNameFontSize = 0f;

    [Header("Initial Score")]
    [SerializeField] private int defaultStartingScore = 0;

    private readonly Dictionary<string, ScoreboardBanner> _bannersByName = new();

    void OnEnable()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (!listParent || !bannerPrefab) return;

        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        _bannersByName.Clear();

        var names = RosterStore.Instance != null ? RosterStore.Instance.Names : null;
        if (names == null || names.Count == 0) return;

        foreach (var rawName in names)
        {
            var go = Instantiate(bannerPrefab, listParent);
            var banner = go.GetComponent<ScoreboardBanner>();
            if (!banner)
            {
                continue;
            }

            banner.SetName(rawName, maxNameFontSize);
            banner.SetScore(defaultStartingScore);

            var colorBinder = go.GetComponent<PlayerBannerColorBinder>();
            if (colorBinder) colorBinder.SetOwnerName(rawName);

            _bannersByName[rawName] = banner;
        }
    }

    public void SetScore(string playerName, int score)
    {
        if (_bannersByName.TryGetValue(playerName, out var b))
            b.SetScore(score);
    }

    public void AddScore(string playerName, int delta)
    {
        if (_bannersByName.TryGetValue(playerName, out var b))
            b.SetScore(b.CurrentScore + delta);
    }

    public void SetAllScores(int score)
    {
        foreach (var kv in _bannersByName)
            kv.Value.SetScore(score);
    }

    public bool TryGetBanner(string playerName, out ScoreboardBanner banner)
        => _bannersByName.TryGetValue(playerName, out banner);
}
