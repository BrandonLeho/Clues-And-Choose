using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardFlip : MonoBehaviour
{
    [Header("References")]
    public RectTransform stackParent;
    public RectTransform frontFacePrefab;

    [Header("Flip Settings")]
    public float flipDuration = 0.35f;
    [Range(0f, 1f)] public float easeOut = 0.7f;
    public bool lockHoverDuringFlip = true;
    public bool oneWayFlip = true;
    public bool allowFlipBack = false;

    bool _isFlipping, _isFrontUp;
    RectTransform _hoverPivot, _flipRig, _backFace, _frontFace;
    Vector3 _basePos;
    Quaternion _baseRot;
    Vector3 _baseScale;

    FrontCardHover3D frontHover;

    public RectTransform CurrentFlipCenter => _flipRig;

    void Reset() => stackParent = transform as RectTransform;

    void Awake()
    {
        if (!stackParent) stackParent = transform as RectTransform;
        frontHover = GetComponent<FrontCardHover3D>();
    }

    public void FlipTopCard()
    {
        if (_isFlipping) return;
        if (!BindTopCard()) return;

        if (oneWayFlip && _isFrontUp) return;
        if (!oneWayFlip && _isFrontUp && !allowFlipBack) return;

        StartCoroutine(CoFlip(_isFrontUp ? 180f : 0f, _isFrontUp ? 0f : 180f));
        _isFrontUp = !_isFrontUp;
    }

    bool BindTopCard()
    {
        RectTransform foundPivot = null;
        if (!stackParent) return false;

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
                var hp = child.transform.Find("HoverPivot") as RectTransform;
                if (hp) foundPivot = hp;
            }
        }

        if (!foundPivot) return false;

        _hoverPivot = foundPivot;

        _basePos = _hoverPivot.anchoredPosition3D;
        _baseRot = _hoverPivot.localRotation;
        _baseScale = _hoverPivot.localScale;

        _flipRig = _hoverPivot.Find("FlipCenter") as RectTransform;
        if (!_flipRig)
        {
            var go = new GameObject("FlipCenter", typeof(RectTransform));
            _flipRig = go.GetComponent<RectTransform>();
            _flipRig.SetParent(_hoverPivot, false);
            _flipRig.anchorMin = Vector2.zero;
            _flipRig.anchorMax = Vector2.one;
            _flipRig.offsetMin = Vector2.zero;
            _flipRig.offsetMax = Vector2.zero;
            _flipRig.pivot = new Vector2(0.5f, 0.5f);
            _flipRig.localScale = Vector3.one;
            _flipRig.localRotation = Quaternion.identity;
            _flipRig.anchoredPosition3D = Vector3.zero;

            if (frontHover) frontHover.SetFlipCenter(_flipRig);

            var toMove = new System.Collections.Generic.List<RectTransform>();
            for (int i = 0; i < _hoverPivot.childCount; i++)
            {
                var c = _hoverPivot.GetChild(i) as RectTransform;
                if (!c || c == _flipRig) continue;
                toMove.Add(c);
            }
            foreach (var c in toMove) c.SetParent(_flipRig, true);
        }

        _backFace = FindFirstActiveGraphic(_flipRig);

        if (!_frontFace)
        {
            if (!frontFacePrefab)
            {
                Debug.LogWarning("[CardFlip] Missing frontFacePrefab.");
                return false;
            }
            _frontFace = Instantiate(frontFacePrefab, _flipRig);
            _frontFace.name = "FrontFace";
            _frontFace.anchorMin = Vector2.zero;
            _frontFace.anchorMax = Vector2.one;
            _frontFace.offsetMin = Vector2.zero;
            _frontFace.offsetMax = Vector2.zero;
            _frontFace.pivot = new Vector2(0.5f, 0.5f);
            _frontFace.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _frontFace.localScale = Vector3.one;

            ToggleRaycast(_frontFace, false);
            ToggleRaycast(_backFace, true);
        }

        _hoverPivot.anchoredPosition3D = _basePos;
        _hoverPivot.localRotation = _baseRot;
        _hoverPivot.localScale = _baseScale;
        _hoverPivot.SetAsLastSibling();

        return true;
    }

    IEnumerator CoFlip(float fromDeg, float toDeg)
    {
        _isFlipping = true;

        bool reEnableFrontHover = false;
        if (frontHover && frontHover.enabled)
        {
            reEnableFrontHover = true;
            frontHover.enabled = false;
        }

        CardHover hover = lockHoverDuringFlip ? GetComponent<CardHover>() : null;
        if (hover)
        {
            hover.LockHover();
            hover.interactable = false;
        }

        float t = 0f;
        float dur = Mathf.Max(0.0001f, flipDuration);

        SetFlipRigY(fromDeg);

        bool frontWillBeUp = toDeg > fromDeg;
        ToggleRaycast(_backFace, !frontWillBeUp);
        ToggleRaycast(_frontFace, frontWillBeUp);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float k = Mathf.Lerp(0f, 3f, easeOut);
            float eased = 1f - Mathf.Pow(1f - p, k + 1f);

            SetFlipRigY(Mathf.LerpUnclamped(fromDeg, toDeg, eased));

            _hoverPivot.anchoredPosition3D = _basePos;
            _hoverPivot.localRotation = _baseRot;
            _hoverPivot.localScale = _baseScale;

            yield return null;
        }

        SetFlipRigY(toDeg);

        bool isFront = Mathf.DeltaAngle(toDeg, 180f) == 0f;
        if (frontHover)
        {
            if (reEnableFrontHover) frontHover.enabled = true;
            frontHover.SetFlipCenter(_flipRig);
            frontHover.SetFrontFacing(isFront);
            frontHover.SetFlipping(false);
        }

        if (hover)
        {
            hover.interactable = true;
            hover.UnlockHover();
        }

        _isFlipping = false;
    }

    void SetFlipRigY(float yDeg)
    {
        if (!_flipRig) return;
        var e = _flipRig.localEulerAngles;
        e.y = yDeg;
        _flipRig.localEulerAngles = e;
    }

    static RectTransform FindFirstActiveGraphic(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var rt = parent.GetChild(i) as RectTransform;
            if (!rt || !rt.gameObject.activeInHierarchy) continue;
            if (rt.GetComponent<Graphic>()) return rt;
        }
        return parent.childCount > 0 ? parent.GetChild(0) as RectTransform : null;
    }

    static void ToggleRaycast(RectTransform face, bool enabled)
    {
        if (!face) return;
        var g = face.GetComponent<Graphic>();
        if (g) g.raycastTarget = enabled;
    }
}
