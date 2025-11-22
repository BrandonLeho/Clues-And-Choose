using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScoreProgressGraph : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] RectTransform graphContainer;
    [SerializeField] float leftPadding = 40f;
    [SerializeField] float rightPadding = 10f;
    [SerializeField] float topPadding = 10f;
    [SerializeField] float bottomPadding = 30f;

    [Header("Prefabs")]
    [SerializeField] GameObject pointPrefab;
    [SerializeField] GameObject linePrefab;

    [Header("Options")]
    [SerializeField] bool autoRefreshOnEnable = true;
    [SerializeField] bool clearOnRefresh = true;

    readonly List<GameObject> _spawnedObjects = new List<GameObject>();

    void OnEnable()
    {
        if (autoRefreshOnEnable)
            Refresh();
    }

    public void Refresh()
    {
        if (!graphContainer || ScoreHistoryRecorder.Instance == null)
            return;

        if (clearOnRefresh)
            ClearGraph();

        var history = ScoreHistoryRecorder.Instance;
        var roundIndices = history.GetRoundIndices();
        if (roundIndices == null || roundIndices.Count == 0)
            return;

        int roundCount = roundIndices.Count;
        int maxScore = 1;

        var allSeries = new List<ScoreHistoryRecorder.PlayerSeries>(history.GetAllSeries());
        if (allSeries.Count == 0)
            return;

        foreach (var series in allSeries)
        {
            foreach (int s in series.scores)
            {
                if (s > maxScore) maxScore = s;
            }
        }

        float width = graphContainer.rect.width - leftPadding - rightPadding;
        float height = graphContainer.rect.height - topPadding - bottomPadding;

        if (width <= 0f || height <= 0f)
            return;

        foreach (var series in allSeries)
        {
            DrawSeries(series, roundCount, maxScore, width, height);
        }
    }

    void DrawSeries(ScoreHistoryRecorder.PlayerSeries series, int roundCount, int maxScore, float width, float height)
    {
        if (series.scores == null || series.scores.Count == 0)
            return;

        Color color;
        if (!RegistryNameColorLookup.TryGetColorForName(series.name, out color))
            color = Color.white;

        Vector2? lastPos = null;
        for (int i = 0; i < series.scores.Count; i++)
        {
            float tX = (roundCount > 1) ? (float)i / (roundCount - 1) : 0.5f;
            float tY = (maxScore > 0) ? (float)series.scores[i] / maxScore : 0f;

            float originX = 0f + leftPadding;
            float originY = 0f + bottomPadding;

            float x = originX + tX * width;
            float y = originY + tY * height;

            Vector2 pointPos = new Vector2(x, y);

            var point = CreatePoint(pointPos, color);

            if (lastPos.HasValue)
            {
                CreateLine(lastPos.Value, pointPos, color);
            }

            lastPos = pointPos;
        }
    }

    GameObject CreatePoint(Vector2 anchoredPos, Color color)
    {
        GameObject obj = pointPrefab ? Instantiate(pointPrefab, graphContainer) : CreateDefaultPoint();
        _spawnedObjects.Add(obj);

        var rt = obj.GetComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;

        rt.anchoredPosition = anchoredPos;

        var img = obj.GetComponent<Image>();
        if (img != null)
            img.color = color;

        return obj;
    }

    GameObject CreateDefaultPoint()
    {
        var obj = new GameObject("Point", typeof(RectTransform), typeof(Image));
        var rt = obj.GetComponent<RectTransform>();
        rt.SetParent(graphContainer, false);
        rt.sizeDelta = new Vector2(6f, 6f);
        return obj;
    }

    void CreateLine(Vector2 start, Vector2 end, Color color)
    {
        GameObject obj = linePrefab ? Instantiate(linePrefab, graphContainer) : CreateDefaultLine();
        _spawnedObjects.Add(obj);

        var rt = obj.GetComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 dir = (end - start);
        float length = dir.magnitude;

        rt.sizeDelta = new Vector2(length, rt.sizeDelta.y <= 0f ? 2f : rt.sizeDelta.y);
        rt.anchoredPosition = (start + end) * 0.5f;
        rt.localRotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);

        var img = obj.GetComponent<Image>();
        if (img != null)
            img.color = color;
    }

    GameObject CreateDefaultLine()
    {
        var obj = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var rt = obj.GetComponent<RectTransform>();
        rt.SetParent(graphContainer, false);
        rt.sizeDelta = new Vector2(10f, 2f);
        return obj;
    }

    public void ClearGraph()
    {
        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            if (_spawnedObjects[i])
                Destroy(_spawnedObjects[i]);
        }
        _spawnedObjects.Clear();
    }
}
