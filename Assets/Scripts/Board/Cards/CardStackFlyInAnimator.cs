using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardStackFlyInAnimator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform cardPrefab;
    [SerializeField] RectTransform stackAnchor;
    [SerializeField] RectTransform spawnParent;

    [Header("Sequence")]
    [Min(1)][SerializeField] int totalCards = 10;
    [SerializeField] float spawnInterval = 0.08f;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Path / Motion")]
    [Min(1f)][SerializeField] float pixelsPerSecond = 1600f;
    [SerializeField] float incomingAngleDeg = 0f;
    [Min(0f)][SerializeField] float startDistanceExtra = 200f;
    [Min(0f)][SerializeField] float pathSpacing = 18f;
    [Min(0f)][SerializeField] float arcHeight = 60f;
    [SerializeField] AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Appearance")]
    [SerializeField] float startRotationZ = 20f;
    [SerializeField] float endRotationZ = 0f;
    [SerializeField] float startScale = 1.0f;
    [SerializeField] float endScale = 1.0f;
    [Range(0, 1)][SerializeField] float startAlpha = 1f;
    [Range(0, 1)][SerializeField] float endAlpha = 1f;

    [Header("Clean-up")]
    [Min(1)][SerializeField] int keepTopN = 1;
    [SerializeField] bool destroyHiddenCards = true;

    [Header("Hooks")]
    public UnityEvent OnSequenceStarted;
    public UnityEvent OnSequenceFinished;

    readonly List<RectTransform> _landed = new List<RectTransform>();
    Coroutine _run;
    int _inFlight;

    void OnDisable()
    {
        if (_run != null)
        {
            StopCoroutine(_run);
            _run = null;
        }
    }

    public void Play() => Play(null);

    public void Play(List<Sprite> sprites)
    {
        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Co_Play(sprites));
    }

    IEnumerator Co_Play(List<Sprite> sprites)
    {
        OnSequenceStarted?.Invoke();

        if (!cardPrefab || !stackAnchor)
            yield break;

        var parent = spawnParent ? spawnParent : stackAnchor.parent as RectTransform;
        if (!parent) parent = stackAnchor;

        Vector2 endPos = stackAnchor.anchoredPosition;
        Vector2 dirOut = DirFromAngle(incomingAngleDeg).normalized;
        Vector2 normal = new Vector2(-dirOut.y, dirOut.x);

        float dist = parent.rect.width + parent.rect.height + startDistanceExtra;
        int count = sprites != null ? sprites.Count : totalCards;

        _inFlight = 0;

        for (int i = 0; i < count; i++)
        {
            float laneIndex = Mathf.Ceil((i + 1) * 0.5f);
            float sign = (i % 2 == 0) ? 1f : -1f;
            float offset = sign * laneIndex * pathSpacing;

            Vector2 start = endPos + dirOut * dist + normal * offset;
            Vector2 control = Vector2.Lerp(start, endPos, 0.5f) + normal * arcHeight;

            Sprite sprite = (sprites != null && i < sprites.Count) ? sprites[i] : null;
            StartCoroutine(Co_SpawnAndFly(parent, start, control, endPos, sprite));

            yield return Wait(spawnInterval);
        }

        while (_inFlight > 0) yield return null;

        OnSequenceFinished?.Invoke();
        _run = null;
    }

    IEnumerator Co_SpawnAndFly(RectTransform parent, Vector2 start, Vector2 control, Vector2 endPos, Sprite sprite)
    {
        _inFlight++;

        RectTransform card = Instantiate(cardPrefab, parent);
        card.gameObject.SetActive(true);
        card.SetAsLastSibling();
        card.anchoredPosition = start;
        card.localRotation = Quaternion.Euler(0, 0, startRotationZ);
        card.localScale = Vector3.one * startScale;
        SetAlphaRecursive(card, startAlpha);

        if (sprite != null)
        {
            var img = card.GetComponentInChildren<Image>(true);
            if (img) img.sprite = sprite;
        }

        float length = ApproxBezierLength(start, control, endPos, 10);
        float dur = Mathf.Max(0.0001f, length / pixelsPerSecond);

        yield return MoveCard(card, start, control, endPos, dur);

        card.anchoredPosition = endPos;
        card.localRotation = Quaternion.Euler(0, 0, endRotationZ);
        card.localScale = Vector3.one * endScale;
        SetAlphaRecursive(card, endAlpha);

        _landed.Add(card);
        PruneStack();

        _inFlight--;
    }

    IEnumerator MoveCard(RectTransform card, Vector2 a, Vector2 c, Vector2 b, float dur)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Delta() / dur;
            float u = easeCurve.Evaluate(Mathf.Clamp01(t));
            Vector2 p = Bezier2(a, c, b, u);
            card.anchoredPosition = p;

            float rot = Mathf.LerpAngle(startRotationZ, endRotationZ, u);
            card.localRotation = Quaternion.Euler(0, 0, rot);

            float sc = Mathf.Lerp(startScale, endScale, u);
            card.localScale = new Vector3(sc, sc, 1f);

            float al = Mathf.Lerp(startAlpha, endAlpha, u);
            SetAlphaRecursive(card, al);

            yield return null;
        }
    }

    void PruneStack()
    {
        if (!destroyHiddenCards) return;
        while (_landed.Count > keepTopN)
        {
            var oldest = _landed[0];
            _landed.RemoveAt(0);
            if (oldest) Destroy(oldest.gameObject);
        }
    }

    float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    object Wait(float s)
    {
        if (s <= 0f) return null;
        return useUnscaledTime ? (object)new WaitForSecondsRealtime(s) : new WaitForSeconds(s);
    }

    static Vector2 DirFromAngle(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    static Vector2 Bezier2(Vector2 a, Vector2 c, Vector2 b, float t)
    {
        Vector2 p0 = Vector2.Lerp(a, c, t);
        Vector2 p1 = Vector2.Lerp(c, b, t);
        return Vector2.Lerp(p0, p1, t);
    }

    static float ApproxBezierLength(Vector2 a, Vector2 c, Vector2 b, int segments)
    {
        float len = 0f;
        Vector2 prev = a;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector2 p = Bezier2(a, c, b, t);
            len += Vector2.Distance(prev, p);
            prev = p;
        }
        return len;
    }

    static void SetAlphaRecursive(RectTransform root, float alpha)
    {
        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var col = images[i].color;
            col.a = alpha;
            images[i].color = col;
        }
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] is Image) continue;
            var col = graphics[i].color;
            col.a = alpha;
            graphics[i].color = col;
        }
    }
}
