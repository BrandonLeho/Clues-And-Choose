using System.Collections.Generic;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class CoinPlacedLock : NetworkBehaviour, ICoinDragPermission
{
    [Header("Lock State (server authoritative)")]
    [SyncVar(hook = nameof(OnLockedChanged))] public bool locked;

    [Header("What to disable while locked")]
    [SerializeField] bool disableColliders = true;
    [SerializeField] bool disableCoinDragHandler = true;
    [SerializeField] bool disableCursorBridge = true;
    [SerializeField] bool disableChangeCursor = true;
    [SerializeField] bool disableRejectionFeedback = true;

    readonly List<(Behaviour comp, bool wasEnabled)> _behaviours = new();
    readonly List<(Collider2D col2d, bool wasEnabled)> _col2D = new();
    readonly List<(Collider col3d, bool wasEnabled)> _col3D = new();

    bool _baselineCached;

    void Awake()
    {
        CacheBaselineIfNeeded();
        ApplyLocked(locked);
    }

    public override void OnStartClient()
    {
        CacheBaselineIfNeeded();
        ApplyLocked(locked);
    }

    public bool CanBeginDrag() => !locked;

    public void SetLocked(bool v)
    {
        if (isServer) locked = v;
        else CmdSetLocked(v);
    }

    [Command(requiresAuthority = false)]
    void CmdSetLocked(bool v) => locked = v;

    void OnLockedChanged(bool _, bool __) => ApplyLocked(locked);

    void ApplyLocked(bool isLocked)
    {
        CacheBaselineIfNeeded();

        if (isLocked)
        {
            for (int i = 0; i < _behaviours.Count; i++) _behaviours[i].comp.enabled = false;

            for (int i = 0; i < _col2D.Count; i++) _col2D[i].col2d.enabled = false;
            for (int i = 0; i < _col3D.Count; i++) _col3D[i].col3d.enabled = false;

            if (disableRejectionFeedback)
            {
                var rej = GetComponent<CoinRejectionFeedback>();
                if (rej) rej.enabled = false;
            }
        }
        else
        {
            for (int i = 0; i < _behaviours.Count; i++)
                _behaviours[i].comp.enabled = _behaviours[i].wasEnabled;

            for (int i = 0; i < _col2D.Count; i++)
                _col2D[i].col2d.enabled = _col2D[i].wasEnabled;
            for (int i = 0; i < _col3D.Count; i++)
                _col3D[i].col3d.enabled = _col3D[i].wasEnabled;

            if (disableRejectionFeedback)
            {
                var rej = GetComponent<CoinRejectionFeedback>();
                if (rej) rej.enabled = true;
            }
        }
    }

    void CacheBaselineIfNeeded()
    {
        if (_baselineCached) return;
        _baselineCached = true;

        _behaviours.Clear();
        _col2D.Clear();
        _col3D.Clear();

        if (disableCoinDragHandler)
        {
            var d = GetComponent<CoinDragHandler>();
            if (d) _behaviours.Add((d, d.enabled));
        }
        if (disableCursorBridge)
        {
            var b = GetComponent<CoinCursorBridge>();
            if (b) _behaviours.Add((b, b.enabled));
        }
        if (disableChangeCursor)
        {
            var c = GetComponent<ChangeCursor>();
            if (c) _behaviours.Add((c, c.enabled));
        }

        if (disableColliders)
        {
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
                _col2D.Add((c, c.enabled));
            foreach (var c in GetComponentsInChildren<Collider>(true))
                _col3D.Add((c, c.enabled));
        }
    }

    void OnDisable()
    {
        if (locked) ApplyLocked(false);
    }
}
