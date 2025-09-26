using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CoinDragPermissionDuringAnim : MonoBehaviour, ICoinDragPermission
{
    [Header("Block while true")]
    public bool blocked;

    [Header("Optional: auto-unblock after a duration")]
    [Min(0f)] public float padSeconds = 0.05f;

    Coroutine _timerCo;

    public bool CanBeginDrag()
    {
        return !blocked;
    }

    public void BeginTempBlock(float duration)
    {
        if (_timerCo != null) StopCoroutine(_timerCo);
        blocked = true;
        _timerCo = StartCoroutine(CoUnblockAfter(duration + padSeconds));
    }

    public void ForceUnblock()
    {
        if (_timerCo != null) StopCoroutine(_timerCo);
        _timerCo = null;
        blocked = false;
    }

    IEnumerator CoUnblockAfter(float t)
    {
        float end = Time.unscaledTime + Mathf.Max(0f, t);
        while (Time.unscaledTime < end) yield return null;
        _timerCo = null;
        blocked = false;
    }
}
