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

    [Header("Reveal Animation")]
    [SerializeField] bool animateLines = true;
    [SerializeField, Min(0f)] float lineRevealDuration = 0.6f;
    [SerializeField] AnimationCurve lineRevealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] bool useUnscaledTime = true;

    [Header("Line Visuals")]
    [SerializeField] float startPadding = 2f;
    [SerializeField] float endPadding = 2f;

    class LineSegmentData
    {
        public Image image;
        public float start;
        public float length;
    }

    class PointData
    {
        public GameObject go;
        public Image image;
        public float distance;
    }

    class SeriesLineData
    {
        public readonly List<LineSegmentData> segments = new List<LineSegmentData>();
        public readonly List<PointData> points = new List<PointData>();
        public float totalLength;
    }

    readonly List<GameObject> _spawnedObjects = new List<GameObject>();
    readonly List<SeriesLineData> _seriesLines = new List<SeriesLineData>();

    Coroutine _revealRoutine;

    void OnEnable()
    {
        if (autoRefreshOnEnable)
            Refresh();
    }

    void OnDisable()
    {
        if (_revealRoutine != null)
        {
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }
    }

    public void Refresh()
    {
        if (!graphContainer || ScoreHistoryRecorder.Instance == null)
            return;

        if (clearOnRefresh)
            ClearGraph();
        else
            ClearLineListOnly();

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

        bool hasAnySegments = false;
        for (int i = 0; i < _seriesLines.Count; i++)
        {
            if (_seriesLines[i].segments.Count > 0)
            {
                hasAnySegments = true;
                break;
            }
        }

        if (animateLines && lineRevealDuration > 0f && hasAnySegments)
        {
            if (_revealRoutine != null)
            {
                StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }
            _revealRoutine = StartCoroutine(CoRevealLines());
        }
        else
        {
            for (int i = 0; i < _seriesLines.Count; i++)
            {
                var seriesLines = _seriesLines[i];

                for (int j = 0; j < seriesLines.segments.Count; j++)
                {
                    if (seriesLines.segments[j].image)
                        seriesLines.segments[j].image.fillAmount = 1f;
                }

                for (int j = 0; j < seriesLines.points.Count; j++)
                {
                    var p = seriesLines.points[j];
                    if (p.go)
                        p.go.SetActive(true);
                    if (p.image)
                    {
                        var c = p.image.color;
                        c.a = 1f;
                        p.image.color = c;
                    }
                }
            }
        }
    }

    void DrawSeries(ScoreHistoryRecorder.PlayerSeries series, int roundCount, int maxScore, float width, float height)
    {
        if (series.scores == null || series.scores.Count == 0)
            return;

        Color color;
        if (!RegistryNameColorLookup.TryGetColorForName(series.name, out color))
            color = Color.white;

        var seriesLines = new SeriesLineData();
        float cumulativeLength = 0f;

        Vector2? lastPos = null;

        for (int i = 0; i < series.scores.Count; i++)
        {
            float tX = (roundCount > 1) ? (float)i / (roundCount - 1) : 0.5f;
            float tY = (maxScore > 0) ? (float)series.scores[i] / maxScore : 0f;

            float originX = leftPadding;
            float originY = bottomPadding;

            float x = originX + tX * width;
            float y = originY + tY * height;

            Vector2 pointPos = new Vector2(x, y);

            if (i == 0)
            {
                var pointGO = CreatePoint(pointPos, color);
                var pointImg = pointGO.GetComponent<Image>();
                seriesLines.points.Add(new PointData
                {
                    go = pointGO,
                    image = pointImg,
                    distance = 0f
                });

                lastPos = pointPos;
                continue;
            }

            float segLen = Vector2.Distance(lastPos.Value, pointPos);
            if (segLen > 0.0001f)
            {
                var img = CreateLine(lastPos.Value, pointPos, color);
                if (img != null)
                {
                    var seg = new LineSegmentData
                    {
                        image = img,
                        start = cumulativeLength,
                        length = segLen
                    };
                    seriesLines.segments.Add(seg);
                    cumulativeLength += segLen;
                }
            }

            var currentPointGO = CreatePoint(pointPos, color);
            var currentPointImg = currentPointGO.GetComponent<Image>();
            seriesLines.points.Add(new PointData
            {
                go = currentPointGO,
                image = currentPointImg,
                distance = cumulativeLength
            });

            lastPos = pointPos;
        }

        seriesLines.totalLength = cumulativeLength;

        if (seriesLines.segments.Count > 0 && seriesLines.totalLength > 0f)
            _seriesLines.Add(seriesLines);
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
        {
            img.color = color;

            if (animateLines && lineRevealDuration > 0f)
            {
                var c = img.color;
                c.a = 0f;
                img.color = c;
            }
        }

        if (animateLines && lineRevealDuration > 0f)
        {
            obj.SetActive(false);
        }

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

    Image CreateLine(Vector2 start, Vector2 end, Color color)
    {
        GameObject obj = linePrefab ? Instantiate(linePrefab, graphContainer) : CreateDefaultLine();
        _spawnedObjects.Add(obj);

        var rt = obj.GetComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 dir = (end - start);
        float geoLength = dir.magnitude;

        float visualLength = geoLength + startPadding + endPadding;
        Vector2 midpoint = (start + end) * 0.5f;
        Vector2 paddingShift = dir.normalized * ((endPadding - startPadding) * 0.5f);

        rt.sizeDelta = new Vector2(visualLength, rt.sizeDelta.y <= 0f ? 2f : rt.sizeDelta.y);
        rt.anchoredPosition = midpoint + paddingShift;
        rt.localRotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);

        var img = obj.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = animateLines && lineRevealDuration > 0f ? 0f : 1f;
        }

        return img;
    }

    GameObject CreateDefaultLine()
    {
        var obj = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var rt = obj.GetComponent<RectTransform>();
        rt.SetParent(graphContainer, false);
        rt.sizeDelta = new Vector2(10f, 2f);
        return obj;
    }

    System.Collections.IEnumerator CoRevealLines()
    {
        for (int i = 0; i < _seriesLines.Count; i++)
        {
            var seriesLines = _seriesLines[i];

            for (int j = 0; j < seriesLines.segments.Count; j++)
            {
                var seg = seriesLines.segments[j];
                if (seg.image)
                    seg.image.fillAmount = 0f;
            }

            for (int j = 0; j < seriesLines.points.Count; j++)
            {
                var p = seriesLines.points[j];
                if (p.go)
                    p.go.SetActive(false);
            }
        }

        float elapsed = 0f;

        while (elapsed < lineRevealDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = (lineRevealDuration > 0f) ? Mathf.Clamp01(elapsed / lineRevealDuration) : 1f;
            float curveT = (lineRevealCurve != null) ? lineRevealCurve.Evaluate(t) : t;

            for (int i = 0; i < _seriesLines.Count; i++)
            {
                var seriesLines = _seriesLines[i];
                float totalLen = seriesLines.totalLength;
                if (totalLen <= 0f) continue;

                float drawDistance = totalLen * curveT;

                for (int j = 0; j < seriesLines.segments.Count; j++)
                {
                    var seg = seriesLines.segments[j];
                    if (!seg.image || seg.length <= 0f)
                        continue;

                    float segStart = seg.start;
                    float segEnd = seg.start + seg.length;

                    float fill;
                    if (drawDistance <= segStart)
                    {
                        fill = 0f;
                    }
                    else if (drawDistance >= segEnd)
                    {
                        fill = 1f;
                    }
                    else
                    {
                        fill = (drawDistance - segStart) / seg.length;
                    }

                    seg.image.fillAmount = Mathf.Clamp01(fill);
                }

                for (int j = 0; j < seriesLines.points.Count; j++)
                {
                    var p = seriesLines.points[j];
                    if (p.go == null) continue;

                    if (drawDistance >= p.distance)
                    {
                        if (!p.go.activeSelf)
                            p.go.SetActive(true);

                        if (p.image)
                        {
                            var c = p.image.color;
                            c.a = 1f;
                            p.image.color = c;
                        }
                    }
                }
            }

            yield return null;
        }

        for (int i = 0; i < _seriesLines.Count; i++)
        {
            var seriesLines = _seriesLines[i];

            for (int j = 0; j < seriesLines.segments.Count; j++)
            {
                var seg = seriesLines.segments[j];
                if (seg.image)
                    seg.image.fillAmount = 1f;
            }

            for (int j = 0; j < seriesLines.points.Count; j++)
            {
                var p = seriesLines.points[j];
                if (p.go)
                    p.go.SetActive(true);
                if (p.image)
                {
                    var c = p.image.color;
                    c.a = 1f;
                    p.image.color = c;
                }
            }
        }

        _revealRoutine = null;
    }

    public void ClearGraph()
    {
        if (_revealRoutine != null)
        {
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }

        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            if (_spawnedObjects[i])
                Destroy(_spawnedObjects[i]);
        }
        _spawnedObjects.Clear();
        _seriesLines.Clear();
    }

    void ClearLineListOnly()
    {
        _seriesLines.Clear();
    }
}