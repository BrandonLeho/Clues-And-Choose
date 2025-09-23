using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Collections;

[DisallowMultipleComponent]
public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Stack")]
    public RectTransform stackParent;

    public bool autoRebindOnChildrenChanged = true;

    [Header("Hover Animation")]
    public Vector2 hoverOffset = new Vector2(16f, 10f);
    public float hoverDepth = -5f;
    public float hoverTiltZ = 8f;
    public bool rotateClockwise = false;
    [Range(1f, 1.2f)] public float hoverScale = 1.04f;

    [Header("Timing & Easing")]
    public float duration = 0.12f;
    [Range(0f, 1f)] public float easeOut = 0.6f;
    public bool interactable = true;

    [Header("Entry Lock")]
    public bool lockedUntilUnlocked = true;
    public float autoUnlockAfter = -1f;

    bool _unlocked;

    [Header("Clue Giver Gate")]
    public bool onlyClueGiverCanInteract = true;

    [Header("Reject Shake (non-clue givers)")]
    public float rejectShakeDuration = 0.15f;
    public float rejectShakeAngle = 6f;
    public float rejectShakeOffset = 8f;
    public int rejectShakeWiggles = 3;
    public float rejectReturnEase = 0.7f;

    [Header("Click")]
    public UnityEvent onClick;

    RectTransform hoverPivot;
    Vector3 _basePos;
    Quaternion _baseRot;
    Vector3 _baseScale;
    Coroutine _anim;

    void Reset()
    {
        stackParent = transform as RectTransform;
    }

    void Awake()
    {
        if (!stackParent) stackParent = transform as RectTransform;
        RebindTopCard();
    }

    void OnEnable()
    {
        if (autoRebindOnChildrenChanged) StartCoroutine(CoWatchChildren());
        _unlocked = !lockedUntilUnlocked;

        if (lockedUntilUnlocked && autoUnlockAfter > 0f)
            StartCoroutine(CoAutoUnlock(autoUnlockAfter));

        SnapToBase();
        RosterStore.OnClueGiverChanged -= HandleClueGiverChanged;
        RosterStore.OnClueGiverChanged += HandleClueGiverChanged;
    }

    void OnDisable()
    {
        if (_anim != null) StopCoroutine(_anim);
        RosterStore.OnClueGiverChanged -= HandleClueGiverChanged;
    }

    public void RebindTopCard()
    {
        hoverPivot = FindTopCard();

        CacheBaseTransform();
    }

    RectTransform FindTopCard()
    {
        if (!stackParent) return null;

        RectTransform found = null;
        int bestIndex = -1;

        for (int i = 0; i < stackParent.childCount; i++)
        {
            var child = stackParent.GetChild(i) as RectTransform;
            if (!child || !child.gameObject.activeInHierarchy) continue;
            if (!child.GetComponent<CardMarker>()) continue;

            int idx = child.GetSiblingIndex();
            if (idx >= bestIndex)
            {
                bestIndex = idx;
                found = child.transform.Find("HoverPivot").GetComponent<RectTransform>();
            }
        }
        return found;
    }

    void CacheBaseTransform()
    {
        if (!hoverPivot) return;
        _basePos = hoverPivot.anchoredPosition3D;
        _baseRot = hoverPivot.localRotation;
        _baseScale = hoverPivot.localScale;
    }

    void SnapToBase()
    {
        if (!hoverPivot) return;
        if (_anim != null) StopCoroutine(_anim);
        hoverPivot.anchoredPosition3D = _basePos;
        hoverPivot.localRotation = _baseRot;
        hoverPivot.localScale = _baseScale;
    }

    IEnumerator CoWatchChildren()
    {
        int lastCount = stackParent ? stackParent.childCount : 0;
        while (enabled)
        {
            if (stackParent && stackParent.childCount != lastCount)
            {
                lastCount = stackParent.childCount;
                var prev = hoverPivot;
                RebindTopCard();

                if (prev && prev != hoverPivot)
                {
                    prev.anchoredPosition3D = _basePos;
                    prev.localRotation = _baseRot;
                    prev.localScale = _baseScale;
                }
            }
            yield return null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interactable || !_unlocked) return;
        if (!hoverPivot) RebindTopCard();
        if (!hoverPivot) return;
        if (!IsLocalClueGiver()) return;

        Play(toHovered: true);
        hoverPivot.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!interactable || !_unlocked || !hoverPivot) return;
        if (!IsLocalClueGiver()) return;

        Play(toHovered: false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable || !_unlocked) return;

        if (!IsLocalClueGiver())
        {
            if (_anim != null) StopCoroutine(_anim);
            StartCoroutine(CoRejectShake());
            return;
        }

        LockHoverKeepPose();
        onClick?.Invoke();
    }

    void Play(bool toHovered)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(toHovered));
    }

    IEnumerator Animate(bool toHovered)
    {
        if (!hoverPivot) yield break;

        Vector3 startPos = hoverPivot.anchoredPosition3D;
        Quaternion startRot = hoverPivot.localRotation;
        Vector3 startScale = hoverPivot.localScale;

        Vector3 targetPos = _basePos + new Vector3(hoverOffset.x, hoverOffset.y, hoverDepth);
        float tilt = rotateClockwise ? -hoverTiltZ : hoverTiltZ;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, tilt) * _baseRot;
        Vector3 targetScale = _baseScale * hoverScale;

        Vector3 endPos = toHovered ? targetPos : _basePos;
        Quaternion endRot = toHovered ? targetRot : _baseRot;
        Vector3 endScale = toHovered ? targetScale : _baseScale;

        float t = 0f;
        float d = Mathf.Max(0.0001f, duration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / d);
            float k = Mathf.Lerp(0f, 3f, easeOut);
            float eased = 1f - Mathf.Pow(1f - p, k + 1f);

            hoverPivot.anchoredPosition3D = Vector3.LerpUnclamped(startPos, endPos, eased);
            hoverPivot.localRotation = Quaternion.Slerp(startRot, endRot, eased);
            hoverPivot.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        hoverPivot.anchoredPosition3D = endPos;
        hoverPivot.localRotation = endRot;
        hoverPivot.localScale = endScale;
        _anim = null;
    }

    IEnumerator CoAutoUnlock(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        UnlockHover();
    }

    public void UnlockHover()
    {
        _unlocked = true;
    }

    public void LockHoverKeepPose()
    {
        _unlocked = false;
        if (_anim != null)
        {
            StopCoroutine(_anim);
            _anim = null;
        }
    }

    IEnumerator CoRejectShake()
    {
        if (!hoverPivot) yield break;

        Vector3 basePos = _basePos;
        Quaternion baseRot = _baseRot;

        float d = Mathf.Max(0.05f, rejectShakeDuration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / d);

            float phase = p * rejectShakeWiggles * Mathf.PI * 2f;
            float s = Mathf.Sin(phase);

            float x = s * rejectShakeOffset;
            float y = 0f;
            float z = 0f;

            float ang = s * rejectShakeAngle;

            hoverPivot.anchoredPosition3D = basePos + new Vector3(x, y, z);
            hoverPivot.localRotation = Quaternion.Euler(0f, 0f, ang) * baseRot;

            yield return null;
        }

        float backT = 0f;
        Vector3 startPos = hoverPivot.anchoredPosition3D;
        Quaternion startRot = hoverPivot.localRotation;
        const float backDur = 0.10f;
        while (backT < backDur)
        {
            backT += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(backT / backDur);
            float k = Mathf.Lerp(0f, 3f, Mathf.Clamp01(rejectReturnEase));
            float eased = 1f - Mathf.Pow(1f - p, k + 1f);

            hoverPivot.anchoredPosition3D = Vector3.LerpUnclamped(startPos, basePos, eased);
            hoverPivot.localRotation = Quaternion.Slerp(startRot, baseRot, eased);
            yield return null;
        }

        hoverPivot.anchoredPosition3D = basePos;
        hoverPivot.localRotation = baseRot;
    }

    public void GetBaseTRS(out Vector3 pos, out Quaternion rot, out Vector3 scale)
    {
        pos = _basePos;
        rot = _baseRot;
        scale = _baseScale;
    }

    bool IsLocalClueGiver()
    {
        if (!onlyClueGiverCanInteract) return true;
        if (string.IsNullOrEmpty(RosterStore.LocalPlayerName)) return false;
        if (string.IsNullOrEmpty(RosterStore.CurrentClueGiverName)) return false;
        return string.Equals(RosterStore.LocalPlayerName, RosterStore.CurrentClueGiverName,
                            System.StringComparison.Ordinal);
    }

    void HandleClueGiverChanged(string _)
    {
        if (!IsLocalClueGiver()) SnapToBase();
    }
}
