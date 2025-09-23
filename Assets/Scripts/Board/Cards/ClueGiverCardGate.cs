using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class ClueGiverCardGate : MonoBehaviour, IPointerClickHandler
{
    [Header("Refs")]
    public CardHover cardHover;
    public CardRejectShaker rejectShaker;
    public RectTransform stackParent;
    [Header("When can the clue giver click?")]
    public bool requireCluePhase = true;

    bool _allowClicks;

    void Reset()
    {
        cardHover = GetComponent<CardHover>();
        rejectShaker = GetComponent<CardRejectShaker>();
        if (!rejectShaker) rejectShaker = gameObject.AddComponent<CardRejectShaker>();
        if (!stackParent && cardHover) rejectShaker.stackParent = cardHover.stackParent;
    }

    void OnEnable()
    {
        RefreshPermission();
        if (GameLoopManager.Exists)
        {
            GameLoopManager.Instance.OnClueGiverChanged.AddListener(_ => RefreshPermission());
            GameLoopManager.Instance.OnPhaseChanged.AddListener(_ => RefreshPermission());
        }
    }

    void OnDisable()
    {
        if (GameLoopManager.Exists)
        {
            GameLoopManager.Instance.OnClueGiverChanged.RemoveListener(_ => RefreshPermission());
            GameLoopManager.Instance.OnPhaseChanged.RemoveListener(_ => RefreshPermission());
        }
    }

    void RefreshPermission()
    {
        _allowClicks = IsLocalClueGiver();
        if (requireCluePhase && GameLoopManager.Exists)
        {
            _allowClicks &= GameLoopManager.Instance != null && NetworkClient.active && true;
        }

        if (cardHover)
            cardHover.allowClick = _allowClicks;
    }

    bool IsLocalClueGiver()
    {
        var localInfo = NetworkClient.localPlayer.GetComponent<NetPlayerInfo>();
        return GameLoopManager.Instance.Client_IsLocalClueGiver(localInfo.netId);

    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_allowClicks) return;
        if (rejectShaker) rejectShaker.Play();
    }
}
