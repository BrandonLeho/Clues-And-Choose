using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public sealed class CoinPlacerDotBadge : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField] Transform listParent;

    [Header("Dot Look")]
    [SerializeField, Min(1f)] float dotRadius = 6f;
    [SerializeField, Min(0f)] float glowThickness = 6f;
    [SerializeField, Range(0f, 1f)] float glowAlpha = 0.4f;
    [SerializeField] Sprite dotSprite;
    [SerializeField] float rightPadding = 16f;
    [SerializeField] float verticalNudge = 0f;

    [Header("Animation")]
    [SerializeField, Min(0f)] float moveDuration = 0.25f;
    [SerializeField] AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField, Min(0f)] float popFadeDuration = 0.15f;

    [Header("Debug")]
    [SerializeField] bool debugLogs;

    readonly Dictionary<string, RectTransform> _nameToLabel = new();

    RectTransform _listParentRt;
    RectTransform _dotRt;
    Image _dotImg;
    RectTransform _glowRt;
    Image _glowImg;
    CanvasGroup _cg;

    bool _hasTargetGate;
    uint _currentPlacerNetId;
    bool _visible;

    Coroutine _transitionCo;
    int _transitionVersion;

    float _columnX;
    bool _columnComputed;
    Color _currentColor = Color.white;

    void Reset()
    {
        if (!listParent && transform.childCount > 0)
            listParent = transform.GetChild(0);
    }

    void OnEnable()
    {
        _listParentRt = listParent ? listParent as RectTransform : transform as RectTransform;
        RebuildNameMap();
        EnsureDot();

        CoinPlacementTurnManager.OnPlacerChangedClient += HandlePlacerChanged;
        PhaseController.OnClientTargetChosen += HandleTargetChosen;

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.AddListener(HandleRoundChanged);
            RoundManager.Instance.onClueGiverChangedClient.AddListener(HandleClueGiverChanged);
        }

        if (CoinPlacementTurnManager.Instance)
            _currentPlacerNetId = CoinPlacementTurnManager.Instance.currentPlacerNetId;

        _hasTargetGate = PhaseController.Instance && PhaseController.Instance.ClientHasTarget;

        UpdateVisibilityAndPosition(immediate: true);
    }

    void OnDisable()
    {
        CoinPlacementTurnManager.OnPlacerChangedClient -= HandlePlacerChanged;
        PhaseController.OnClientTargetChosen -= HandleTargetChosen;

        if (RoundManager.Instance)
        {
            RoundManager.Instance.onRoundChangedClient.RemoveListener(HandleRoundChanged);
            RoundManager.Instance.onClueGiverChangedClient.RemoveListener(HandleClueGiverChanged);
        }

        KillTransition();
    }

    void HandlePlacerChanged(uint netId)
    {
        _currentPlacerNetId = netId;
        if (debugLogs) Debug.Log($"[DotBadge] Placer changed → {_currentPlacerNetId}");
        UpdateVisibilityAndPosition(immediate: false);
    }

    void HandleTargetChosen(int col, int row, Color color)
    {
        _hasTargetGate = true;
        if (debugLogs) Debug.Log("[DotBadge] Target chosen → badge gate enabled");
        UpdateVisibilityAndPosition(immediate: false);
    }

    void HandleRoundChanged(int _, uint __)
    {
        _hasTargetGate = false;
        if (debugLogs) Debug.Log("[DotBadge] Round changed → hide & reset");
        UpdateVisibilityAndPosition(immediate: false);
    }

    void HandleClueGiverChanged(uint ___)
    {
        _hasTargetGate = false;
        if (debugLogs) Debug.Log("[DotBadge] Clue giver changed → hide & reset");
        UpdateVisibilityAndPosition(immediate: false);
    }

    void RebuildNameMap()
    {
        _nameToLabel.Clear();
        _columnComputed = false;
        _columnX = 0f;

        if (!listParent) return;
        _listParentRt = listParent as RectTransform;

        float maxRightLocalX = 0f;
        bool any = false;

        for (int i = 0; i < listParent.childCount; i++)
        {
            var row = listParent.GetChild(i);
            var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (!label) continue;

            string n = label.text?.Trim();
            if (string.IsNullOrWhiteSpace(n)) continue;

            _nameToLabel[n] = label.rectTransform;
            any = true;

            if (_listParentRt != null)
            {
                Rect r = label.rectTransform.rect;
                Vector3 rightWorld = label.rectTransform.TransformPoint(new Vector3(r.xMax, r.center.y, 0f));
                Vector3 rightLocal = _listParentRt.InverseTransformPoint(rightWorld);
                maxRightLocalX = Mathf.Max(maxRightLocalX, rightLocal.x);
            }
        }

        if (any && _listParentRt != null)
        {
            _columnX = maxRightLocalX + rightPadding;
            _columnComputed = true;
        }
    }

    void EnsureDot()
    {
        if (_dotRt) return;

        if (!_listParentRt)
            _listParentRt = listParent ? listParent as RectTransform : transform as RectTransform;

        var dotGo = new GameObject("CoinPlacerDot", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dotGo.transform.SetParent(_listParentRt ? _listParentRt : transform as RectTransform, false);
        _dotRt = (RectTransform)dotGo.transform;
        _dotImg = dotGo.GetComponent<Image>();
        _cg = dotGo.GetComponent<CanvasGroup>();
        _cg.alpha = 0f;

        _dotImg.raycastTarget = false;
        _dotImg.sprite = dotSprite != null
            ? dotSprite
            : Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        _dotImg.type = Image.Type.Simple;
        _dotImg.preserveAspect = true;

        _dotRt.anchorMin = _dotRt.anchorMax = _dotRt.pivot = new Vector2(0.5f, 0.5f);
        _dotRt.anchoredPosition = Vector2.zero;

        var glowGo = new GameObject("CoinPlacerDotGlow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(_dotRt, false);
        _glowRt = (RectTransform)glowGo.transform;
        _glowImg = glowGo.GetComponent<Image>();
        _glowImg.raycastTarget = false;
        _glowImg.sprite = _dotImg.sprite;
        _glowImg.type = Image.Type.Simple;
        _glowImg.preserveAspect = true;
        _glowRt.anchorMin = _glowRt.anchorMax = _glowRt.pivot = new Vector2(0.5f, 0.5f);

        ApplyDotSizing();
        HideImmediate();
    }

    void ApplyDotSizing()
    {
        if (!_dotRt) return;

        float d = dotRadius * 2f;
        _dotRt.sizeDelta = new Vector2(d, d);

        if (_glowRt)
        {
            float g = (dotRadius + glowThickness) * 2f;
            _glowRt.sizeDelta = new Vector2(g, g);
        }
    }

    void HideImmediate()
    {
        if (!_dotRt) return;
        _cg.alpha = 0f;
        _dotRt.gameObject.SetActive(false);
        _visible = false;
    }

    void UpdateVisibilityAndPosition(bool immediate)
    {
        EnsureDot();

        if (!_hasTargetGate || _currentPlacerNetId == 0)
        {
            HideAnimated(immediate);
            return;
        }

        if (!RosterStore.TryGetNameByNetId(_currentPlacerNetId, out var placerName))
        {
            if (debugLogs) Debug.Log("[DotBadge] Could not resolve placer name; popping out.");
            HideAnimated(immediate);
            return;
        }

        if (!_nameToLabel.TryGetValue(placerName, out var labelRt) || !labelRt)
        {
            RebuildNameMap();
            if (!_nameToLabel.TryGetValue(placerName, out labelRt) || !labelRt)
            {
                if (debugLogs) Debug.Log($"[DotBadge] No label found for {placerName}; popping out.");
                HideAnimated(immediate);
                return;
            }
        }

        if (!_columnComputed)
            RebuildNameMap();

        if (!_columnComputed || _listParentRt == null)
        {
            if (debugLogs) Debug.Log("[DotBadge] Column not computed; popping out.");
            HideAnimated(immediate);
            return;
        }

        Color targetColor;
        if (!RegistryNameColorLookup.TryGetColorForName(placerName, out targetColor))
            targetColor = Color.white;

        Rect lr = labelRt.rect;
        Vector3 centerWorld = labelRt.TransformPoint(new Vector3(lr.center.x, lr.center.y, 0f));
        Vector3 centerLocal = _listParentRt.InverseTransformPoint(centerWorld);

        Vector2 targetPos = new Vector2(_columnX, centerLocal.y + verticalNudge);

        if (immediate)
        {
            KillTransition();
            _dotRt.gameObject.SetActive(true);
            _dotRt.localPosition = new Vector3(targetPos.x, targetPos.y, _dotRt.localPosition.z);
            _cg.alpha = 1f;
            SetDotColor(targetColor);
            _visible = true;
            return;
        }

        if (!_visible)
            PopIn(targetPos, targetColor);
        else
            SlideTo(targetPos, targetColor);
    }

    void HideAnimated(bool immediate)
    {
        if (immediate || !_visible)
        {
            KillTransition();
            HideImmediate();
            return;
        }

        KillTransition();
        _transitionVersion++;
        _transitionCo = StartCoroutine(Co_PopOut(_transitionVersion));
    }

    void PopIn(Vector2 targetPos, Color targetColor)
    {
        KillTransition();
        _transitionVersion++;
        _transitionCo = StartCoroutine(Co_PopIn(targetPos, targetColor, _transitionVersion));
    }

    void SlideTo(Vector2 targetPos, Color targetColor)
    {
        KillTransition();
        _transitionVersion++;
        _transitionCo = StartCoroutine(Co_SlideTo(targetPos, targetColor, _transitionVersion));
    }

    void SetDotColor(Color c)
    {
        _currentColor = c;
        if (_dotImg) _dotImg.color = c;
        if (_glowImg) _glowImg.color = new Color(c.r, c.g, c.b, glowAlpha);
    }

    IEnumerator Co_PopIn(Vector2 targetPos, Color targetColor, int version)
    {
        _dotRt.gameObject.SetActive(true);
        _dotRt.localPosition = new Vector3(targetPos.x, targetPos.y, _dotRt.localPosition.z);

        float dur = popFadeDuration <= 0f ? 0.001f : popFadeDuration;

        Color fromColor = _currentColor;
        Color toColor = targetColor;

        float t = 0f;
        float startA = 0f;
        float endA = 1f;

        while (t < dur)
        {
            if (version != _transitionVersion) yield break;

            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);

            Color c = Color.LerpUnclamped(fromColor, toColor, u);
            SetDotColor(c);
            _cg.alpha = Mathf.LerpUnclamped(startA, endA, u);

            yield return null;
        }

        if (version != _transitionVersion) yield break;

        SetDotColor(toColor);
        _cg.alpha = endA;
        _visible = true;
    }

    IEnumerator Co_PopOut(int version)
    {
        float dur = popFadeDuration <= 0f ? 0.001f : popFadeDuration;

        float t = 0f;
        float startA = _cg.alpha;
        float endA = 0f;

        while (t < dur)
        {
            if (version != _transitionVersion) yield break;

            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            _cg.alpha = Mathf.LerpUnclamped(startA, endA, u);

            yield return null;
        }

        if (version != _transitionVersion) yield break;

        _cg.alpha = 0f;
        _dotRt.gameObject.SetActive(false);
        _visible = false;
    }

    IEnumerator Co_SlideTo(Vector2 targetPos, Color targetColor, int version)
    {
        float dur = moveDuration <= 0f ? 0.001f : moveDuration;

        Vector2 fromPos = new Vector2(_dotRt.localPosition.x, _dotRt.localPosition.y);
        Color fromColor = _currentColor;
        Color toColor = targetColor;

        float t = 0f;

        _cg.alpha = 1f;
        _dotRt.gameObject.SetActive(true);
        _visible = true;

        while (t < dur)
        {
            if (version != _transitionVersion) yield break;

            t += Time.unscaledDeltaTime;

            float u = Mathf.Clamp01(t / dur);
            float e = moveCurve != null ? moveCurve.Evaluate(u) : u;

            Vector2 newPos = Vector2.LerpUnclamped(fromPos, targetPos, e);
            _dotRt.localPosition = new Vector3(newPos.x, newPos.y, _dotRt.localPosition.z);

            Color c = Color.LerpUnclamped(fromColor, toColor, e);
            SetDotColor(c);

            yield return null;
        }

        if (version != _transitionVersion) yield break;

        _dotRt.localPosition = new Vector3(targetPos.x, targetPos.y, _dotRt.localPosition.z);
        SetDotColor(toColor);
        _cg.alpha = 1f;
        _visible = true;
    }

    void KillTransition()
    {
        if (_transitionCo != null)
            StopCoroutine(_transitionCo);

        _transitionCo = null;
    }

    public void NotifyListRebuilt()
    {
        RebuildNameMap();
        UpdateVisibilityAndPosition(immediate: true);
    }
}
