using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ColorChoosingPhaseController : NetworkBehaviour
{
    [Header("Chooser (CanvasGroup on the WRAPPER)")]
    public CanvasGroup chooserGroup;

    [Header("Slide Settings")]
    public float slideDuration = 0.5f;
    public float offscreenMargin = 80f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool deactivateChooserOnEnd = true;

    [Header("Game Group (enable after choosing)")]
    public CanvasGroup gameGroup;
    public bool hideGameWhileChoosing = true;
    public bool deactivateGameWhileChoosing = false;
    public bool letEntryAnimatorControlVisibility = true;

    [Header("Handoff (legacy toggle)")]
    public bool activateGameRootsOnEnd = false;
    public GameObject[] gameRoots;
    public UnityEvent onPhaseStarted;
    public UnityEvent onPhaseEnded;

    [Header("Exit Timing")]
    public float exitDelaySeconds = 1f;
    bool _exitStarted;

    ColorLockRegistry registry;
    bool serverPhaseEnded;

    RectTransform canvasRT;
    RectTransform wrapperRT;
    Vector2 wrapperHome = Vector2.zero;
    Coroutine anim;

    void Awake()
    {
        canvasRT = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        if (!canvasRT) canvasRT = FindFirstObjectByType<Canvas>()?.GetComponent<RectTransform>();

        if (!chooserGroup)
        {
#if UNITY_2023_1_OR_NEWER
            var picker = Object.FindFirstObjectByType<SelectionController>(FindObjectsInactive.Include);
#else
            var picker = Object.FindObjectOfType<SelectionController>(true);
#endif
            if (picker)
            {
                chooserGroup = picker.GetComponentInParent<CanvasGroup>(true);
            }
        }

        if (!chooserGroup) return;

        chooserGroup.ignoreParentGroups = true;
        chooserGroup.gameObject.SetActive(true);
        chooserGroup.interactable = false;
        chooserGroup.blocksRaycasts = false;

        InitGameGroupDisabled();

        wrapperRT = chooserGroup.GetComponent<RectTransform>();
        if (!wrapperRT) return;

        wrapperRT.anchorMin = Vector2.zero;
        wrapperRT.anchorMax = Vector2.one;
        wrapperRT.pivot = new Vector2(0.5f, 0.5f);
        wrapperRT.anchoredPosition = wrapperHome;
        wrapperRT.sizeDelta = Vector2.zero;

        wrapperRT.anchoredPosition = OffscreenBelow(wrapperHome);
    }

    void Start()
    {
        if (!chooserGroup || !wrapperRT) return;
        StartCoroutine(Co_EnterAfterLayout());
    }

    IEnumerator Co_EnterAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        wrapperRT.anchoredPosition = OffscreenBelow(wrapperHome);
        yield return null;

        SlideTo(wrapperHome, () =>
        {
            chooserGroup.interactable = true;
            chooserGroup.blocksRaycasts = true;
            onPhaseStarted?.Invoke();
        });
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(Server_WaitAndHookRegistry());
    }

    IEnumerator Server_WaitAndHookRegistry()
    {
        float t = 3f;
        while (t > 0f && !registry)
        {
            registry = ColorLockRegistry.Instance;
            if (registry) break;
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (!registry) yield break;

        registry.OnRegistryChanged += Server_MaybeEndPhase;
        yield return null;
        Server_MaybeEndPhase();
    }

    void OnDestroy()
    {
        if (isServer && registry != null)
            registry.OnRegistryChanged -= Server_MaybeEndPhase;
    }

    void Server_MaybeEndPhase()
    {
        if (serverPhaseEnded || registry == null) return;

        int players = NetworkServer.spawned.Values.Count(id => id && id.GetComponent<PlayerNameSync>());
        int chosen = registry.colorByOwner.Count;

        if (players > 0 && chosen >= players)
        {
            serverPhaseEnded = true;
            RpcEndChoosingPhase();
        }
    }

    [ClientRpc]
    void RpcEndChoosingPhase()
    {
        if (!chooserGroup || !wrapperRT) return;
        if (_exitStarted) return;
        _exitStarted = true;

        chooserGroup.interactable = false;
        chooserGroup.blocksRaycasts = false;

        StartCoroutine(Co_ExitAfterDelay());
    }

    IEnumerator Co_ExitAfterDelay()
    {
        float wait = Mathf.Max(0f, exitDelaySeconds);
        if (wait > 0f) yield return new WaitForSecondsRealtime(wait);

        EnableGameGroup();

        SlideTo(OffscreenBelow(wrapperHome), () =>
        {
            if (deactivateChooserOnEnd) chooserGroup.gameObject.SetActive(false);

            if (activateGameRootsOnEnd && gameRoots != null)
                foreach (var go in gameRoots) if (go) go.SetActive(true);

            onPhaseEnded?.Invoke();
        });
    }

    void InitGameGroupDisabled()
    {
        if (!gameGroup) return;

        if (!gameGroup.gameObject.activeSelf && !deactivateGameWhileChoosing)
            gameGroup.gameObject.SetActive(true);

        if (hideGameWhileChoosing) gameGroup.alpha = 0f;
        gameGroup.interactable = false;
        gameGroup.blocksRaycasts = false;

        if (deactivateGameWhileChoosing)
            gameGroup.gameObject.SetActive(false);
    }

    void EnableGameGroup()
    {
        if (!gameGroup) return;

        if (!gameGroup.gameObject.activeSelf)
            gameGroup.gameObject.SetActive(true);

        if (letEntryAnimatorControlVisibility)
        {
            if (hideGameWhileChoosing) gameGroup.alpha = 0f;
            gameGroup.interactable = false;
            gameGroup.blocksRaycasts = false;
        }
        else
        {
            if (hideGameWhileChoosing) gameGroup.alpha = 1f;
            gameGroup.interactable = true;
            gameGroup.blocksRaycasts = true;
        }
    }


    Vector2 OffscreenBelow(Vector2 home)
    {
        float h = canvasRT ? canvasRT.rect.height : Screen.height;
        return home + new Vector2(0f, -(h + offscreenMargin));
    }

    void SlideTo(Vector2 target, System.Action then = null)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Co_Slide(target, then));
    }

    IEnumerator Co_Slide(Vector2 target, System.Action then)
    {
        Vector2 start = wrapperRT.anchoredPosition;
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = ease.Evaluate(Mathf.Clamp01(t / slideDuration));
            wrapperRT.anchoredPosition = Vector2.LerpUnclamped(start, target, p);
            yield return null;
        }
        wrapperRT.anchoredPosition = target;
        then?.Invoke();
    }
}
