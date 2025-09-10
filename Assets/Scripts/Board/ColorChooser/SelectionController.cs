// SelectionController.cs  (additions)
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Events;

public class SelectionController : MonoBehaviour
{
    [System.Serializable]
    public class ColorChosenEvent : UnityEvent<Color, int> { }

    [Header("UI")]
    public List<ColorSwatch> swatches = new List<ColorSwatch>();
    public Button confirmButton;

    [Header("Cancel UI")]
    [SerializeField] Button cancelButton;

    [Header("Events")]
    public ColorChosenEvent onColorConfirmed;
    public UnityEvent onCancelLockRequested = new UnityEvent();

    [Header("Multiplayer")]
    [Tooltip("When true, Confirm only requests a lock from server; actual lock is applied from network callbacks.")]
    public bool networkAuthoritative = true;

    ColorSwatch _current;
    ColorSwatch _locked;

    void Awake()
    {
        if (confirmButton) confirmButton.onClick.AddListener(ConfirmCurrent);
        if (cancelButton) cancelButton.onClick.AddListener(CancelLockClicked);

        for (int i = 0; i < swatches.Count; i++)
        {
            var s = swatches[i];
            if (!s) continue;
            s.owner = this;
            s.SetSelected(false);
        }

        UpdateConfirmCancelUI();
    }

    public void Select(ColorSwatch swatch)
    {
        if (!swatch) return;
        if (swatch.IsLocked && swatch != _locked) return;

        _current = swatch;
        foreach (var s in swatches) if (s) s.SetSelected(s == swatch);
        UpdateConfirmCancelUI();
    }

    void ConfirmCurrent()
    {
        if (_current == null) return;

        if (networkAuthoritative)
        {
            int idx = swatches.IndexOf(_current);
            onColorConfirmed?.Invoke(_current.GetFillColor(), idx);
            _current.SetSelected(true);
            return;
        }

        if (_current == _locked && _locked != null) return;
        if (_locked != null) { _locked.Unlock(); _locked.SetSelected(false); }

        _current.Lock();
        _locked = _current;

        int i2 = swatches.IndexOf(_current);
        onColorConfirmed?.Invoke(_current.GetFillColor(), i2);
        _current.SetSelected(true);

        UpdateConfirmCancelUI();
    }


    public void CancelCurrent()
    {
        if (!HasPendingSelection()) return;
        _current.SetSelected(false);
        _current = null;
        UpdateConfirmInteractable();
    }

    public void ClearSelection()
    {
        if (_current) _current.SetSelected(false);
        _current = null;
        foreach (var s in swatches) if (s) s.SetSelected(false);
        UpdateConfirmCancelUI();
    }

    public void ClearLock()
    {
        if (_locked != null) _locked.Unlock();
        _locked = null;
        UpdateConfirmCancelUI();
    }

    void CancelLockClicked()
    {
        if (_locked == null) return;

        if (_current) { _current.SetSelected(false); }
        if (_locked) { _locked.SetSelected(false); }

        _current = null;

        UpdateConfirmCancelUI();
        cancelButton.interactable = false;
        onCancelLockRequested?.Invoke();
    }

    void UpdateConfirmInteractable()
    {
        bool canConfirm = (_current != null && _current != _locked);

        if (confirmButton) confirmButton.interactable = canConfirm;

        if (cancelButton) cancelButton.gameObject.SetActive(canConfirm);
    }

    void UpdateConfirmCancelUI()
    {
        bool hasLocked = (_locked != null);
        bool canConfirm = (!hasLocked && _current != null);

        if (confirmButton)
        {
            confirmButton.gameObject.SetActive(!hasLocked);
            confirmButton.interactable = canConfirm;
        }

        if (cancelButton)
        {
            cancelButton.gameObject.SetActive(hasLocked);
            cancelButton.interactable = hasLocked;
        }
    }

    public void SetLockedFromNetwork(int index)
    {
        if (index < 0 || index >= swatches.Count) return;
        var s = swatches[index];

        if (_locked != null && _locked != s)
        {
            _locked.Unlock();
            _locked.SetSelected(false);
        }

        s.Lock();
        s.SetSelected(true);
        _locked = s;

        UpdateConfirmCancelUI();
    }

    public void SetSwatchLockedState(int index, bool locked)
    {
        if (index < 0 || index >= swatches.Count) return;
        var s = swatches[index]; if (!s) return;

        if (locked && !s.IsLocked) s.Lock();
        if (!locked && s.IsLocked) s.Unlock();

        if (_current == s && locked && s != _locked)
        {
            s.SetSelected(false);
            _current = null;
        }

        if (!locked && _locked == s)
        {
            s.SetSelected(false);
            _locked = null;
        }

        UpdateConfirmCancelUI();
    }

    public bool TryGetCurrentSwatch(out ColorSwatch swatch)
    {
        swatch = null;
        if (_current != null && _current != _locked)
        {
            swatch = _current;
            return true;
        }
        return false;
    }

    public bool CanConfirmNow() => (_current != null && _current != _locked);

    public bool HasPendingSelection() => (_current != null && _current != _locked);

    public void SetOwnerName(int index, string owner)
    {
        if (index < 0 || index >= swatches.Count || !swatches[index]) return;
        swatches[index].ShowOwnerName(owner);
    }
    public void ClearOwnerName(int index)
    {
        if (index < 0 || index >= swatches.Count || !swatches[index]) return;
        swatches[index].HideOwnerName();
    }
}
