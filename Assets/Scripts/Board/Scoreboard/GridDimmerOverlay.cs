using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class GridDimmerOverlay : MonoBehaviour
{
    static GridDimmerOverlay _instance;
    public static GridDimmerOverlay Instance
    {
        get
        {
            if (_instance) return _instance;

            var found = FindFirstObjectByType<GridDimmerOverlay>(FindObjectsInactive.Include);

            if (found) _instance = found;
            return _instance;
        }
        private set { _instance = value; }
    }

    [SerializeField] Image overlay;
    [SerializeField, Range(0f, 1f)] float darkAlpha = 0.7f;
    [SerializeField] float fadeIn = 0.25f;
    [SerializeField] float fadeOut = 0.25f;
    [SerializeField] float revealDelayAfterBanner = 1.0f;
    [SerializeField] bool blockRaycastsWhileDark = true;
    [SerializeField, Min(1)] int totalRows = 16;
    [SerializeField] bool flipRowsForHover = true;
    [SerializeField] Transform gridRoot;


    int _col = -1, _row = -1; Color _color = Color.white;
    Coroutine _co;
    CanvasGroup _cg;

    void Awake()
    {
        if (!Instance) Instance = this;
        EnsureOverlayRefs();
        if (_cg) { _cg.alpha = 0f; _cg.blocksRaycasts = blockRaycastsWhileDark; _cg.interactable = false; }
    }

    void OnEnable()
    {
        if (!Instance) Instance = this;
        PhaseController.OnClientTargetChosen += HandleTargetChosen;
    }

    void OnDisable()
    {
        PhaseController.OnClientTargetChosen -= HandleTargetChosen;
    }

    void EnsureOverlayRefs()
    {
        if (!overlay) overlay = GetComponent<Image>() ?? GetComponentInChildren<Image>(true);
        if (overlay)
        {
            _cg = overlay.GetComponent<CanvasGroup>();
            if (!_cg) _cg = overlay.gameObject.AddComponent<CanvasGroup>();
        }
    }

    void HandleTargetChosen(int col, int row, Color color)
    { _col = col; _row = row; _color = color; }

    public void FadeInDuringScoring()
    {
        var inst = Instance; if (!inst) return;

        inst.DisableAllCellHoversNow();

        if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);
        inst.EnsureOverlayRefs();
        if (!inst._cg) return;

        if (inst._co != null) inst.StopCoroutine(inst._co);
        inst._co = inst.StartCoroutine(inst.CoFade(1f, inst.fadeIn));
    }


    public void OnScoringBannerFinished()
    {
        var inst = Instance; if (!inst) return;
        inst.StartCoroutine(inst.CoRevealAfterDelay());
    }

    IEnumerator CoRevealAfterDelay()
    {
        float t = 0f;
        while (t < revealDelayAfterBanner) { t += Time.deltaTime; yield return null; }

        var relay = GridHoverRelay.Instance;
        if (relay != null && _col >= 0 && _row >= 0)
        {
            int uiRow = _row;
            if (flipRowsForHover)
                uiRow = Mathf.Clamp(totalRows - 1 - uiRow, 0, totalRows - 1);

            relay.HoverEnter(_col, uiRow, _color);

            var rings = GridRingRevealer.Instance;
            if (rings != null)
                rings.Begin(_col, uiRow, keepOnlyChosenHoverEnabled: true);
        }
        //FadeOut();
    }


    public void FadeOut()
    {
        if (!_cg) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFade(0f, fadeOut));
    }

    IEnumerator CoFade(float target, float dur)
    {
        float start = _cg.alpha, t = 0f, d = Mathf.Max(0.0001f, dur);
        _cg.blocksRaycasts = blockRaycastsWhileDark && target > 0.01f;

        while (t < d)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            float goal = (target > 0.5f) ? darkAlpha : 0f;
            _cg.alpha = Mathf.Lerp(start, goal, u);
            yield return null;
        }
        _cg.alpha = (target > 0.5f) ? darkAlpha : 0f;
    }

    void DisableAllCellHoversNow()
    {
        GridHoverRelay.Instance?.HoverExit();

        var hovers = FindObjectsByType<GridCellHoverWithCoords>(FindObjectsSortMode.None);
        for (int i = 0; i < hovers.Length; i++)
        {
            var h = hovers[i];
            if (!h) continue;

            if (h.IsHoverLocked)
            {
                h.SetHoverLock(false, keepShown: false);
                h.ProbeEnter();
            }

            h.ProbeExit();

            h.SetHoverEnabled(false);
        }

        if (gridRoot)
        {
            var enablers = gridRoot.GetComponents<EnableAllCellHoversAfterFlyIn>();
            for (int i = 0; i < enablers.Length; i++)
                if (enablers[i]) enablers[i].enabled = false;
        }
    }

    void EnableAllCellHovers()
    {
        if (!gridRoot) return;
        var hovers = gridRoot.GetComponentsInChildren<GridCellHoverWithCoords>(true);
        for (int i = 0; i < hovers.Length; i++) hovers[i]?.SetHoverEnabled(true);

        var enablers = gridRoot.GetComponents<EnableAllCellHoversAfterFlyIn>();
        for (int i = 0; i < enablers.Length; i++) if (enablers[i]) enablers[i].enabled = true;
    }
}
