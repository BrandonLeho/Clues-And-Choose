using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class SelectionController : MonoBehaviour
{
    [System.Serializable]
    public class ColorChosenEvent : UnityEvent<Color, int> { } // (color, swatchIndex)

    [Header("UI")]
    public List<ColorSwatch> swatches = new List<ColorSwatch>();
    public Button confirmButton;

    [Header("Events")]
    public ColorChosenEvent onColorConfirmed;   // subscribe from game code

    ColorSwatch _current;       // currently highlighted/selected (not yet locked)
    ColorSwatch _locked;        // the swatch this player has locked

    void Awake()
    {
        if (confirmButton) confirmButton.onClick.AddListener(ConfirmCurrent);

        for (int i = 0; i < swatches.Count; i++)
        {
            if (!swatches[i]) continue;
            swatches[i].owner = this;
            swatches[i].SetSelected(false);
        }

        UpdateConfirmInteractable();
    }

    // Called by ColorSwatch when clicked
    public void Select(ColorSwatch swatch)
    {
        if (swatch == null) return;
        if (swatch.IsLocked && swatch != _locked) return; // cannot select a color locked by someone else

        _current = swatch;

        foreach (var s in swatches)
            if (s) s.SetSelected(s == swatch);

        UpdateConfirmInteractable();
    }

    void ConfirmCurrent()
    {
        if (_current == null) return;

        // If the current is already the locked one, nothing to do.
        if (_current == _locked && _locked != null) return;

        // 1) Re-enable the previously locked swatch (if any)
        if (_locked != null)
        {
            _locked.Unlock();                 // restores interactable + hover
            _locked.SetSelected(false);       // clear its selected visuals
        }

        // 2) Lock the newly selected swatch
        _current.Lock();
        _locked = _current;

        // 3) Notify listeners (doesn't change colors)
        int idx = swatches.IndexOf(_current);
        onColorConfirmed?.Invoke(_current.GetFillColor(), idx);

        // Keep it visually selected (optional)
        _current.SetSelected(true);

        UpdateConfirmInteractable();
    }

    public void ClearSelection()
    {
        _current = null;
        foreach (var s in swatches) if (s) s.SetSelected(false);
        UpdateConfirmInteractable();
    }

    public void ClearLock()  // optional API if you ever need to free it programmatically
    {
        if (_locked != null) _locked.Unlock();
        _locked = null;
        UpdateConfirmInteractable();
    }

    void UpdateConfirmInteractable()
    {
        if (!confirmButton) return;
        // Can confirm iff something is selected and it's not already the locked one
        confirmButton.interactable = (_current != null && _current != _locked);
    }

    // In ColorPickerUI
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

    // ADD inside SelectionController:
    public void ApplyLockFromNetwork(int swatchIndex, bool asLocalPlayer)
    {
        if (swatchIndex < 0 || swatchIndex >= swatches.Count) return;
        var sw = swatches[swatchIndex];
        if (!sw) return;

        // unlock the previously locked (if different)
        if (_locked != null && _locked != sw)
            _locked.Unlock();

        // set + show lock
        _locked = sw;
        _locked.Lock();

        // keep it visually selected and fire your existing event only for the owner
        if (asLocalPlayer)
        {
            _current = _locked;
            _locked.SetSelected(true);
            onColorConfirmed?.Invoke(_locked.GetFillColor(), swatchIndex);
        }
        UpdateConfirmInteractable();
    }

    public void ApplyUnlockFromNetwork(int swatchIndex)
    {
        if (swatchIndex < 0 || swatchIndex >= swatches.Count) return;
        var sw = swatches[swatchIndex];
        if (!sw) return;

        bool wasMine = (sw == _locked);
        sw.Unlock();
        if (wasMine) _locked = null;
        UpdateConfirmInteractable();
    }


}
