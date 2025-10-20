using UnityEngine;
using TMPro;
using UnityEngine.UI;

public sealed class ScoreFloatText : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform rect;
    [SerializeField] TMP_Text text;

    [Header("Anim")]
    [SerializeField, Min(0f)] float travelSeconds = 0.65f;
    [SerializeField, Min(0f)] float holdSeconds = 0.15f;
    [SerializeField] AnimationCurve moveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] AnimationCurve popScale = AnimationCurve.EaseInOut(0, 0, 1, 1);

    Vector2 _start;
    RectTransform _target;
    float _t;
    bool _armed;

    public void Init(Vector2 screenStart, RectTransform target, int points, TMP_FontAsset overrideFont = null)
    {
        if (!rect) rect = transform as RectTransform;
        if (text && overrideFont) text.font = overrideFont;

        _target = target;
        _start = screenStart;
        if (rect) rect.position = screenStart;

        if (text) text.text = (points >= 0 ? "+" : "") + points.ToString();

        _t = 0f;
        _armed = true;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (!_armed || !_target) return;

        _t += Time.deltaTime;
        float d = Mathf.Max(0.0001f, travelSeconds);
        float u = Mathf.Clamp01(_t / d);

        Vector2 goal = _target.position;
        Vector2 pos = Vector2.LerpUnclamped(_start, goal, moveEase.Evaluate(u));
        rect.position = pos;

        float s = 1f + 0.35f * popScale.Evaluate(Mathf.Clamp01(_t / (travelSeconds * 0.6f)));
        rect.localScale = new Vector3(s, s, 1f);

        if (_t >= travelSeconds + holdSeconds)
            Destroy(gameObject);
    }
}
