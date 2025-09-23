using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CardRejectShaker : MonoBehaviour
{
    [Header("Target")]
    public RectTransform explicitTarget;
    public RectTransform stackParent;

    [Header("Shake")]
    public float duration = 0.18f;
    public float amplitude = 12f;
    public int oscillations = 3;
    public float tiltZ = 5f;

    Coroutine _co;

    public void Play()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoShake());
    }

    IEnumerator CoShake()
    {
        var target = ResolveTarget();
        if (!target) yield break;

        var basePos = target.anchoredPosition3D;
        var baseRot = target.localRotation;

        float t = 0f;
        float d = Mathf.Max(0.0001f, duration);

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / d);

            float wave = Mathf.Sin(p * Mathf.PI * 2f * oscillations); // -1..1
            Vector3 offset = new Vector3(wave * amplitude, 0f, 0f);
            float z = wave * tiltZ;

            target.anchoredPosition3D = basePos + offset;
            target.localRotation = Quaternion.Euler(0f, 0f, z) * baseRot;

            yield return null;
        }

        target.anchoredPosition3D = basePos;
        target.localRotation = baseRot;
        _co = null;
    }

    RectTransform ResolveTarget()
    {
        if (explicitTarget) return explicitTarget;
        if (!stackParent) return null;

        RectTransform found = null;
        int bestIndex = -1;

        for (int i = 0; i < stackParent.childCount; i++)
        {
            var child = stackParent.GetChild(i) as RectTransform;
            if (!child || !child.gameObject.activeInHierarchy) continue;
            int idx = child.GetSiblingIndex();
            if (idx >= bestIndex)
            {
                bestIndex = idx;
                var pivot = child.Find("HoverPivot") as RectTransform;
                if (pivot) found = pivot;
            }
        }
        return found;
    }
}
