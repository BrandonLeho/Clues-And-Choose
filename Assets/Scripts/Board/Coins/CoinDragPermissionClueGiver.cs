using Mirror;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(NetworkCoin))]
public class CoinDragPermissionClueGiver : MonoBehaviour, ICoinDragPermission
{
    [Header("Rejection Feedback")]
    public float shakeDuration = 0.18f;
    public float shakeMagnitude = 0.07f;
    public AnimationCurve shakeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    NetworkCoin _coin;
    Coroutine _shake;

    void Awake() => _coin = GetComponent<NetworkCoin>();

    public bool CanBeginDrag()
    {
        if (ClueGiverState.IsLocalPlayerClueGiver() && _coin != null && _coin.IsLocalOwner())
        {
            PlayShake();
            return false;
        }
        return true;
    }

    void PlayShake()
    {
        if (_shake != null) StopCoroutine(_shake);
        _shake = StartCoroutine(Shake());
    }

    IEnumerator Shake()
    {
        var t = 0f;
        var start = transform.localPosition;
        while (t < shakeDuration)
        {
            float p = t / shakeDuration;
            float amp = shakeMagnitude * (1f - shakeEase.Evaluate(p));
            float x = (Mathf.PerlinNoise(100f, t * 60f) - 0.5f) * 2f * amp;
            float y = (Mathf.PerlinNoise(200f, t * 60f) - 0.5f) * 2f * amp;
            transform.localPosition = start + new Vector3(x, y, 0f);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        transform.localPosition = start;
        _shake = null;
    }
}
