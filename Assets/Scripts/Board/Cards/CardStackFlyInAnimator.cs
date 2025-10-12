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

    [Header("Slide-Out")]
    [Min(1f)][SerializeField] float slideOutPixelsPerSecond = 1600f;
    [Min(0f)][SerializeField] float slideOutDistanceExtra = 250f;
    [Range(0, 1)][SerializeField] float slideOutEndAlpha = 0f;
    [SerializeField] bool slideOutUsesLocalLeft = true;
    [SerializeField] float slideOutTiltDeg = -12f;
    [Range(0f, 1f)][SerializeField] float tiltBlend = 1f;
    [SerializeField] bool tiltOverDuration = true;
    [SerializeField] AnimationCurve slideOutEaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] int respawnSiblingIndex = 1;


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

        ToggleRaycastTargets(card, false);

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

    public void PlaySlideOutAndRespawn()
    {
        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Co_SlideOutAndRespawn());
    }

    IEnumerator Co_SlideOutAndRespawn()
    {
        if (_landed == null || _landed.Count == 0 || !stackAnchor)
        {
            _run = null;
            yield break;
        }

        var card = _landed[_landed.Count - 1];
        _landed.RemoveAt(_landed.Count - 1);
        if (!card)
        {
            _run = null;
            yield break;
        }

        var parent = spawnParent ? spawnParent : (stackAnchor.parent as RectTransform);
        if (!parent) parent = stackAnchor;

        Vector2 endPos = stackAnchor.anchoredPosition;
        Vector2 dirOut = slideOutUsesLocalLeft ? LocalLeftInParentSpace(card, parent) : Vector2.left;

        float dist = parent.rect.width + parent.rect.height + slideOutDistanceExtra;
        Vector2 target = endPos + dirOut * dist;

        float dur = Mathf.Max(0.0001f, dist / Mathf.Max(1f, slideOutPixelsPerSecond));

        float startZ = card.localEulerAngles.z;
        float targetZ = Mathf.LerpAngle(startZ, slideOutTiltDeg, Mathf.Clamp01(tiltBlend));

        float t = 0f;
        while (t < 1f)
        {
            t += Delta() / dur;
            float rawU = Mathf.Clamp01(t);
            float u = slideOutEaseCurve.Evaluate(rawU);

            card.anchoredPosition = Vector2.Lerp(endPos, target, u);

            float a = Mathf.Lerp(endAlpha, slideOutEndAlpha, u);
            SetAlphaRecursive(card, a);

            float z = tiltOverDuration ? Mathf.LerpAngle(startZ, targetZ, u) : targetZ;
            card.localRotation = Quaternion.Euler(0f, 0f, z);

            yield return null;
        }

        if (card) Destroy(card.gameObject);

        RectTransform newCard = Instantiate(cardPrefab, parent);

        if (!newCard.gameObject.activeSelf)
            newCard.gameObject.SetActive(true);

        newCard.anchorMin = stackAnchor.anchorMin;
        newCard.anchorMax = stackAnchor.anchorMax;
        newCard.pivot = stackAnchor.pivot;
        newCard.anchoredPosition = stackAnchor.anchoredPosition;
        newCard.localRotation = stackAnchor.localRotation;
        newCard.localScale = stackAnchor.localScale;

        int desired = Mathf.Clamp(respawnSiblingIndex, 0, Mathf.Max(0, parent.childCount - 1));
        newCard.SetSiblingIndex(desired);

        SetAlphaRecursive(newCard, endAlpha);
        ToggleRaycastTargets(card, false);

        var hover = parent.GetComponent<CardHover>();
        if (hover) hover.RebindTopCard();

        if (_landed != null) _landed.Add(newCard);

        _run = null;
    }

    Vector2 LocalLeftInParentSpace(RectTransform child, RectTransform parent)
    {
        Vector3 worldLeft = child.TransformDirection(Vector3.left);
        Vector3 parentDir = parent.InverseTransformDirection(worldLeft);
        Vector2 d = new Vector2(parentDir.x, parentDir.y);
        if (d.sqrMagnitude < 0.000001f) d = Vector2.left;
        return d.normalized;
    }

    static void ToggleRaycastTargets(RectTransform root, bool enabled)
    {
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = enabled;
    }
}
