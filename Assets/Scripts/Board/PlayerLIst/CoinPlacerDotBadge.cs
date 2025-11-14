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
    [SerializeField, Min(0f)] float fadeDuration = 0.15f;

    [Header("Debug")]
    [SerializeField] bool debugLogs;

    readonly Dictionary<string, RectTransform> _nameToLabel = new();
    RectTransform _dotRt;
    Image _dotImg;
    RectTransform _glowRt;
    Image _glowImg;
    CanvasGroup _cg;
    bool _hasTargetGate;
    uint _currentPlacerNetId;
    Coroutine _moveCo;
    int _moveVersion;

    void Reset()
    {
        if (!listParent && transform.childCount > 0) listParent = transform.GetChild(0);
    }

    void OnEnable()
    {
        RebuildNameMap();

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

        EnsureDot();
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

        KillMove();
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
        if (!listParent) return;

        for (int i = 0; i < listParent.childCount; i++)
        {
            var row = listParent.GetChild(i);
            var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (!label) continue;
            string n = label.text?.Trim();
            if (string.IsNullOrWhiteSpace(n)) continue;
            _nameToLabel[n] = label.rectTransform;
        }
    }

    void EnsureDot()
    {
        if (_dotRt) return;

        var dotGo = new GameObject("CoinPlacerDot", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dotGo.transform.SetParent(listParent ? listParent : transform, worldPositionStays: false);
        _dotRt = (RectTransform)dotGo.transform;
        _dotImg = dotGo.GetComponent<Image>();
        _cg = dotGo.GetComponent<CanvasGroup>();
        _cg.alpha = 0f;

        _dotImg.raycastTarget = false;
        _dotImg.sprite = dotSprite != null ? dotSprite : UnityEngine.Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        _dotImg.type = Image.Type.Simple;
        _dotImg.preserveAspect = true;

        _dotRt.anchorMin = new Vector2(0, 1);
        _dotRt.anchorMax = new Vector2(0, 1);
        _dotRt.pivot = new Vector2(0.5f, 0.5f);
        _dotRt.anchoredPosition = Vector2.zero;

        var glowGo = new GameObject("CoinPlacerDotGlow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(_dotRt, worldPositionStays: false);
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
    }

    void UpdateVisibilityAndPosition(bool immediate)
    {
        EnsureDot();

        if (!_hasTargetGate || _currentPlacerNetId == 0)
        {
            AnimateHide(immediate);
            return;
        }

        if (!RosterStore.TryGetNameByNetId(_currentPlacerNetId, out var placerName))
        {
            if (debugLogs) Debug.Log("[DotBadge] Could not resolve placer name; hiding.");
            AnimateHide(immediate);
            return;
        }

        if (!_nameToLabel.TryGetValue(placerName, out var labelRt) || !labelRt)
        {
            RebuildNameMap();
            if (!_nameToLabel.TryGetValue(placerName, out labelRt) || !labelRt)
            {
                if (debugLogs) Debug.Log($"[DotBadge] No label found for {placerName}; hiding.");
                AnimateHide(immediate);
                return;
            }
        }

        Color c;
        if (!RegistryNameColorLookup.TryGetColorForName(placerName, out c))
            c = Color.white;
        _dotImg.color = c;
        _glowImg.color = new Color(c.r, c.g, c.b, glowAlpha);

        var rowParent = labelRt.parent as RectTransform;
        var targetParent = rowParent ? rowParent : listParent as RectTransform;

        float labelW = labelRt.rect.width;
        var tmp = labelRt.GetComponent<TextMeshProUGUI>();
        if (tmp) labelW = Mathf.Max(labelW, tmp.preferredWidth);

        Vector3 worldTarget = labelRt.TransformPoint(new Vector3(labelW + rightPadding, verticalNudge, 0));

        if (_dotRt.parent != targetParent)
        {
            Vector3 worldBefore = _dotRt.position;
            _dotRt.SetParent(targetParent, worldPositionStays: false);
            _dotRt.position = worldBefore;
        }

        Vector2 anchoredTarget;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_dotRt.parent,
            RectTransformUtility.WorldToScreenPoint(null, worldTarget),
            null, out anchoredTarget);

        AnimateShowTo(anchoredTarget, immediate);
    }

    void AnimateShowTo(Vector2 anchoredTarget, bool immediate)
    {
        _dotRt.gameObject.SetActive(true);

        KillMove();
        _moveVersion++;

        if (immediate)
        {
            _dotRt.anchoredPosition = anchoredTarget;
            _cg.alpha = 1f;
            return;
        }

        _moveCo = StartCoroutine(Co_MoveAndFade(anchoredTarget, show: true, version: _moveVersion));
    }

    void AnimateHide(bool immediate)
    {
        KillMove();
        _moveVersion++;

        if (!immediate && _dotRt.gameObject.activeSelf)
        {
            _moveCo = StartCoroutine(Co_MoveAndFade(Vector2.zero, show: false, version: _moveVersion));
        }
        else
        {
            HideImmediate();
        }
    }

    IEnumerator Co_MoveAndFade(Vector2 target, bool show, int version)
    {
        Vector2 fromPos = _dotRt.anchoredPosition;
        float moveT = 0f, fadeT = 0f;

        float moveDur = moveDuration <= 0f ? 0.001f : moveDuration;
        float fadeDur = fadeDuration <= 0f ? 0.001f : fadeDuration;

        float startA = _cg.alpha;
        float endA = show ? 1f : 0f;

        while (moveT < moveDur || fadeT < fadeDur)
        {
            if (version != _moveVersion) yield break;
            float dt = Time.unscaledDeltaTime;

            moveT += dt;
            fadeT += dt;

            float mu = Mathf.Clamp01(moveT / moveDur);
            float fu = Mathf.Clamp01(fadeT / fadeDur);
            float me = moveCurve != null ? moveCurve.Evaluate(mu) : mu;

            _dotRt.anchoredPosition = Vector2.LerpUnclamped(fromPos, target, me);
            _cg.alpha = Mathf.LerpUnclamped(startA, endA, fu);

            yield return null;
        }

        if (version != _moveVersion) yield break;

        _dotRt.anchoredPosition = target;
        _cg.alpha = endA;

        if (!show)
            _dotRt.gameObject.SetActive(false);
    }

    void KillMove()
    {
        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = null;
    }

    public void NotifyListRebuilt()
    {
        RebuildNameMap();
        UpdateVisibilityAndPosition(immediate: true);
    }
}
