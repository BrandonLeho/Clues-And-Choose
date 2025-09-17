using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;

public class ClueGiverRoulette : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text nameItemPrefab;
    [SerializeField] private RectTransform centerMarker;

    [Header("Layout")]
    [SerializeField] private float itemWidth = 280f;
    [SerializeField] private float itemHeight = 80f;
    [SerializeField] private float spacing = 16f;
    [SerializeField] private float scaleAtCenter = 1.15f;
    [SerializeField] private float scaleFalloff = 0.0025f;

    [Header("Spin")]
    [SerializeField] private float startSpeed = 2000f;
    [SerializeField] private float minCycles = 2.0f;
    [SerializeField] private float decelDuration = 1.75f;
    [SerializeField] private AnimationCurve decelCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Snap")]
    [SerializeField] private float snapDuration = 0.4f;
    [SerializeField] private AnimationCurve snapCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Result UI (optional)")]
    [SerializeField] private TMP_Text resultLabel;

    public event Action<string> OnSelected;

    readonly List<TMP_Text> _items = new();
    readonly List<string> _names = new();

    float _itemFull;
    float _contentWidth;
    bool _spinning;
    int _targetIndex = -1;

    void Awake()
    {
        _itemFull = itemWidth + spacing;
    }

    public void BuildFromNames(IList<string> names)
    {
        Clear();
        if (names == null || names.Count == 0) return;

        _names.AddRange(names);

        int loops = 3;
        for (int k = 0; k < loops; k++)
        {
            foreach (var n in _names)
            {
                var t = Instantiate(nameItemPrefab, content);
                t.text = n;
                var rt = (RectTransform)t.transform;
                rt.sizeDelta = new Vector2(itemWidth, itemHeight);
                _items.Add(t);
            }
        }

        LayoutImmediate();
        CenterOnIndex(_names.Count);
        UpdateItemScales();
    }

    public void StartRoulette(int? forcedIndex = null)
    {
        if (_spinning || _items.Count == 0) return;
        _targetIndex = forcedIndex.HasValue
            ? Mathf.Clamp(forcedIndex.Value, 0, _names.Count - 1)
            : -1;
        StartCoroutine(SpinRoutine());
    }

    void LayoutImmediate()
    {
        var h = content.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (h) h.spacing = spacing;

        _contentWidth = _items.Count * _itemFull - spacing;
        var cSize = content.sizeDelta;
        content.sizeDelta = new Vector2(_contentWidth, Mathf.Max(cSize.y, itemHeight));
        content.anchoredPosition = new Vector2(0f, 0f);
    }

    IEnumerator SpinRoutine()
    {
        _spinning = true;
        resultLabel?.SetText(string.Empty);

        float passedPixels = 0f;
        float speed = startSpeed;

        float targetX;

        float needPixels = minCycles * _contentWidth / 3f;
        while (passedPixels < needPixels)
        {
            float dx = speed * Time.deltaTime;
            MoveBy(-dx);
            passedPixels += dx;
            UpdateItemScales();
            yield return null;
        }

        int winnerLogicalIndex = (_targetIndex >= 0)
            ? _targetIndex
            : Random.Range(0, _names.Count);

        int middleLoopOffset = _names.Count;
        int winnerIndexInItems = middleLoopOffset + winnerLogicalIndex;

        float centerX = GetCenterWorldX();
        targetX = ComputeCenteringAnchoredPosX(winnerIndexInItems, centerX);

        Vector2 startPos = content.anchoredPosition;
        float startX = startPos.x;
        float endX = targetX;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / decelDuration;
            float p = decelCurve.Evaluate(Mathf.Clamp01(t));
            float x = Mathf.Lerp(startX, endX, p);
            SetContentX(x);
            UpdateItemScales();
            yield return null;
        }

        float exactX = ComputeCenteringAnchoredPosX(winnerIndexInItems, centerX);
        startX = content.anchoredPosition.x;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / snapDuration;
            float p = snapCurve.Evaluate(Mathf.Clamp01(t));
            float x = Mathf.Lerp(startX, exactX, p);
            SetContentX(x);
            UpdateItemScales();
            yield return null;
        }

        string winner = _names[winnerLogicalIndex];
        resultLabel?.SetText(winner);
        Debug.Log("UR WINNER: " + winner);
        OnSelected?.Invoke(winner);
        _spinning = false;
    }

    void MoveBy(float dx)
    {
        float x = content.anchoredPosition.x + dx;
        SetContentX(x);

        float leftEdge = content.TransformPoint(Vector3.zero).x;
        float viewLeft = viewport.TransformPoint(new Vector3(0, 0, 0)).x;

        while (viewLeft - leftEdge > _itemFull)
        {
            var first = _items[0].rectTransform;
            first.SetAsLastSibling();
            _items.RemoveAt(0);
            _items.Add(first.GetComponent<TMP_Text>());
            SetContentX(content.anchoredPosition.x + _itemFull);
            leftEdge += _itemFull;
        }
    }

    void CenterOnIndex(int indexInItems)
    {
        float centerX = GetCenterWorldX();
        float x = ComputeCenteringAnchoredPosX(indexInItems, centerX);
        SetContentX(x);
    }

    float ComputeCenteringAnchoredPosX(int indexInItems, float viewportCenterX)
    {
        var tgt = _items[indexInItems].rectTransform;
        var world = tgt.TransformPoint(new Vector3(tgt.rect.width * 0.5f, 0, 0)).x;

        float worldDelta = viewportCenterX - world;
        return content.anchoredPosition.x + WorldDeltaToContentAnchoredDelta(worldDelta);
    }

    float WorldDeltaToContentAnchoredDelta(float worldDelta)
    {
        return worldDelta;
    }

    float GetCenterWorldX()
    {
        if (centerMarker)
            return centerMarker.TransformPoint(Vector3.zero).x;

        var v = viewport;
        var worldCenter = v.TransformPoint(new Vector3(v.rect.width * 0.5f, 0, 0));
        return worldCenter.x;
    }

    void SetContentX(float x)
    {
        content.anchoredPosition = new Vector2(x, content.anchoredPosition.y);
    }

    void UpdateItemScales()
    {
        float cx = GetCenterWorldX();

        foreach (var t in _items)
        {
            var rt = t.rectTransform;
            float tx = rt.TransformPoint(new Vector3(rt.rect.width * 0.5f, 0, 0)).x;
            float d = Mathf.Abs(tx - cx);
            float s = Mathf.Max(1f, scaleAtCenter - d * scaleFalloff);
            rt.localScale = Vector3.one * s;
        }
    }

    void Clear()
    {
        foreach (var t in _items)
            if (t) Destroy(t.gameObject);
        _items.Clear();
        _names.Clear();
        content.anchoredPosition = Vector2.zero;
    }
}
