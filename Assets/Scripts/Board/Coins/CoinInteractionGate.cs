using System.Collections.Generic;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class CoinInteractionGate : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnBlockUntilChanged))]
    double _blockUntilServerTime = 0;

    readonly List<(Behaviour comp, bool wasEnabled)> _behaviours = new();
    readonly List<(Collider2D col, bool wasEnabled)> _col2D = new();
    readonly List<(Collider col, bool wasEnabled)> _col3D = new();

    [SerializeField] bool disableColliders = true;
    [SerializeField] bool disableCoinDragHandler = true;
    [SerializeField] bool disableCursorBridge = true;
    [SerializeField] bool disableChangeCursor = true;

    bool _gateActive;
    bool _hasBaselineCache;

    [Server]
    public void ArmForSeconds(float seconds)
    {
        var newUntil = NetworkTime.time + Mathf.Max(0f, seconds);
        if (newUntil > _blockUntilServerTime)
            _blockUntilServerTime = newUntil;

        if (isServer) ApplyOrSchedule();
    }

    void OnBlockUntilChanged(double _, double __) => ApplyOrSchedule();

    public override void OnStartClient() => ApplyOrSchedule();

    void Update()
    {
        if (_gateActive && NetworkTime.time >= _blockUntilServerTime)
            EndGate();
    }

    void ApplyOrSchedule()
    {
        if (NetworkTime.time < _blockUntilServerTime)
            StartGate();
        else
            EndGate();
    }

    void StartGate()
    {
        if (!_hasBaselineCache)
        {
            CacheTargets();
            _hasBaselineCache = true;
        }

        if (!_gateActive)
        {
            SetEnabled(false);
            _gateActive = true;
        }
    }

    void EndGate()
    {
        if (!_gateActive) return;
        _gateActive = false;

        RestoreEnabled();
    }

    void CacheTargets()
    {
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
            var cc = GetComponent<ChangeCursor>();
            if (cc) _behaviours.Add((cc, cc.enabled));
        }

        if (disableColliders)
        {
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
                _col2D.Add((c, c.enabled));
            foreach (var c in GetComponentsInChildren<Collider>(true))
                _col3D.Add((c, c.enabled));
        }
    }

    void SetEnabled(bool enable)
    {
        for (int i = 0; i < _behaviours.Count; i++)
            _behaviours[i].comp.enabled = enable;

        for (int i = 0; i < _col2D.Count; i++)
            _col2D[i].col.enabled = enable;

        for (int i = 0; i < _col3D.Count; i++)
            _col3D[i].col.enabled = enable;
    }

    void RestoreEnabled()
    {
        for (int i = 0; i < _behaviours.Count; i++)
            _behaviours[i].comp.enabled = _behaviours[i].wasEnabled;

        for (int i = 0; i < _col2D.Count; i++)
            _col2D[i].col.enabled = _col2D[i].wasEnabled;

        for (int i = 0; i < _col3D.Count; i++)
            _col3D[i].col.enabled = _col3D[i].wasEnabled;
    }

    void OnDisable()
    {
        if (_gateActive)
            RestoreEnabled();
    }
}
