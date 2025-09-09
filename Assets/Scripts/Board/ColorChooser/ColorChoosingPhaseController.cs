// ColorChoosingPhaseController.cs  (chooser-only slide, wrapper-based)
using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ColorChoosingPhaseController : NetworkBehaviour
{
    [Header("Chooser (CanvasGroup on the WRAPPER)")]
    public CanvasGroup chooserGroup;            // put on ChooserWrapper

    [Header("Slide Settings")]
    public float slideDuration = 0.5f;          // your 0.5s
    public float offscreenMargin = 80f;         // pixels below the canvas
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool deactivateChooserOnEnd = true;

    [Header("Handoff")]
    public bool activateGameRootsOnEnd = false; // no animation here; your system handles it
    public GameObject[] gameRoots;
    public UnityEvent onPhaseStarted;           // after chooser slides IN
    public UnityEvent onPhaseEnded;             // after chooser slides OUT

    ColorLockRegistry registry;
    bool serverPhaseEnded;

    RectTransform canvasRT;
    RectTransform wrapperRT;     // the thing we animate (ChooserWrapper)
    Vector2 wrapperHome = Vector2.zero; // always (0,0) with stretch anchors
    Coroutine anim;

    void Awake()
    {
        // canvas size for off-screen calc
        canvasRT = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        if (!canvasRT) canvasRT = FindObjectOfType<Canvas>()?.GetComponent<RectTransform>();

        if (!chooserGroup)
        {
#if UNITY_2023_1_OR_NEWER
            var picker = Object.FindFirstObjectByType<SelectionController>(FindObjectsInactive.Include);
#else
            var picker = Object.FindObjectOfType<SelectionController>(true);
#endif
            if (picker)
            {
                // Take a CanvasGroup from a parent — we want the WRAPPER’s CanvasGroup
                chooserGroup = picker.GetComponentInParent<CanvasGroup>(true);
            }
        }

        if (!chooserGroup)
        {
            Debug.LogWarning("[ColorChoosingPhase] chooserGroup not set.");
            return;
        }

        chooserGroup.ignoreParentGroups = true;
        chooserGroup.gameObject.SetActive(true);
        chooserGroup.interactable = false;
        chooserGroup.blocksRaycasts = false;

        wrapperRT = chooserGroup.GetComponent<RectTransform>();
        if (!wrapperRT)
        {
            Debug.LogWarning("[ColorChoosingPhase] chooserGroup needs a RectTransform.");
            return;
        }

        // Wrapper must be full-screen and centered so home = (0,0)
        wrapperRT.anchorMin = Vector2.zero;
        wrapperRT.anchorMax = Vector2.one;
        wrapperRT.pivot = new Vector2(0.5f, 0.5f);
        wrapperRT.anchoredPosition = wrapperHome;
        wrapperRT.sizeDelta = Vector2.zero;

        // Start off-screen (but WAIT to slide until Start coroutine)
        wrapperRT.anchoredPosition = OffscreenBelow(wrapperHome);
    }

    void Start()
    {
        if (!chooserGroup || !wrapperRT) return;
        StartCoroutine(Co_EnterAfterLayout());
    }

    IEnumerator Co_EnterAfterLayout()
    {
        // let UI and scaler settle
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        // ensure we start off-screen using the *current* canvas height
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

        chooserGroup.interactable = false;
        chooserGroup.blocksRaycasts = false;

        SlideTo(OffscreenBelow(wrapperHome), () =>
        {
            if (deactivateChooserOnEnd) chooserGroup.gameObject.SetActive(false);

            if (activateGameRootsOnEnd && gameRoots != null)
                foreach (var go in gameRoots) if (go) go.SetActive(true);

            onPhaseEnded?.Invoke();
        });
    }

    // -------- slide helpers --------
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
