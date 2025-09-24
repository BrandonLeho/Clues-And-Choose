using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class CoinRejectionFeedback : MonoBehaviour
{
    [Header("Shake")]
    public float duration = 0.16f;
    public float magnitude = 0.07f;
    public AnimationCurve easeOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional flash on SpriteRenderer")]
    public SpriteRenderer sprite;
    public float flashBoost = 0.35f;
    public float flashFade = 0.12f;

    Coroutine _shakeCo;
    Vector3 _startLocal;

    void Awake()
    {
        _startLocal = transform.localPosition;
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
    }

    public void Play()
    {
        if (_shakeCo != null) StopCoroutine(_shakeCo);
        _shakeCo = StartCoroutine(CoShake());
    }

    IEnumerator CoShake()
    {
        float t = 0f;
        var basePos = _startLocal = transform.localPosition;

        if (sprite) StartCoroutine(CoFlash(sprite, flashBoost, flashFade));

        while (t < duration)
        {
            float p = t / duration;
            float amp = magnitude * (1f - easeOut.Evaluate(p));
            float x = (Mathf.PerlinNoise(100f, t * 80f) - 0.5f) * 2f * amp;
            float y = (Mathf.PerlinNoise(200f, t * 80f) - 0.5f) * 2f * amp;
            transform.localPosition = basePos + new Vector3(x, y, 0f);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        transform.localPosition = basePos;
        _shakeCo = null;
    }

    IEnumerator CoFlash(SpriteRenderer sr, float boost, float fade)
    {
        float t = 0f;
        var c0 = sr.color;
        var c1 = new Color(
            Mathf.Clamp01(c0.r + boost),
            Mathf.Clamp01(c0.g + boost),
            Mathf.Clamp01(c0.b + boost),
            c0.a
        );
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - (t / fade);
            sr.color = Color.Lerp(c0, c1, a);
            yield return null;
        }
        sr.color = c0;
    }
}
