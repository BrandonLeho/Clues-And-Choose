using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ClueGiverCardGate : MonoBehaviour, IPointerClickHandler
{
    [Header("Refs")]
    public CardHover cardHover;
    public CardRejectShaker rejectShaker;
    public RectTransform stackParent;

    [Header("Rules")]
    public bool requireCluePhase = false;
    private bool _allowClicks;
    private string _lastPhase;

    private UnityAction<string> _phaseListener;
    private UnityAction<string> _clueListener;

    void Reset()
    {
        cardHover = GetComponent<CardHover>();
        rejectShaker = GetComponent<CardRejectShaker>();
        if (!rejectShaker) rejectShaker = gameObject.AddComponent<CardRejectShaker>();
    }

    void Awake()
    {
        if (!cardHover) cardHover = GetComponent<CardHover>();
        if (rejectShaker && !rejectShaker.stackParent && stackParent)
            rejectShaker.stackParent = stackParent;

        SetAllow(false);
    }

    void OnEnable()
    {
        if (GameLoopManager.Exists)
        {
            _phaseListener = OnPhaseChanged;
            _clueListener = OnClueGiverChanged;
            GameLoopManager.Instance.OnPhaseChanged.AddListener(_phaseListener);
            GameLoopManager.Instance.OnClueGiverChanged.AddListener(_clueListener);
        }

        RefreshPermission();
        StartCoroutine(CoDeferredRefresh());
    }

    System.Collections.IEnumerator CoDeferredRefresh()
    {
        yield return null;
        RefreshPermission();
        yield return null;
        RefreshPermission();
    }

    void OnDisable()
    {
        if (GameLoopManager.Exists)
        {
            if (_phaseListener != null)
                GameLoopManager.Instance.OnPhaseChanged.RemoveListener(_phaseListener);
            if (_clueListener != null)
                GameLoopManager.Instance.OnClueGiverChanged.RemoveListener(_clueListener);
        }
    }

    private void OnPhaseChanged(string phase)
    {
        _lastPhase = phase;
        RefreshPermission();
    }

    private void OnClueGiverChanged(string name)
    {
        RefreshPermission();
    }

    private void RefreshPermission()
    {
        bool allow = SafeIsLocalClueGiver();
        cardHover.allowClick = _allowClicks;

        if (requireCluePhase)
            allow &= _lastPhase == "Clue";

        SetAllow(allow);
    }

    private void SetAllow(bool allow)
    {
        _allowClicks = allow;
        if (cardHover != null)
        {
            // Maybe TODO idk yet
        }
    }

    private bool SafeIsLocalClueGiver()
    {
        if (!GameLoopManager.Exists) return false;
        if (!NetworkClient.active) return false;
        if (!NetworkClient.localPlayer) return false;

        var localInfo = NetworkClient.localPlayer.GetComponent<NetPlayerInfo>();
        if (!localInfo) return false;

        return GameLoopManager.Instance.Client_IsLocalClueGiver(localInfo.netId);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[Gate] allow={_allowClicks}");

        if (_allowClicks) return;
        if (rejectShaker) rejectShaker.Play();
        eventData.Use();
    }
}
