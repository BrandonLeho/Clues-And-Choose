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

    [Header("Multiplayer")]
    [Tooltip("When true, Confirm only requests a lock from server; actual lock is applied from network callbacks.")]
    public bool networkAuthoritative = true;

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

        if (networkAuthoritative)
        {
            // In network mode, just ask whoever is listening (ColorPickerMirrorBinder)
            int idx = swatches.IndexOf(_current);
            onColorConfirmed?.Invoke(_current.GetFillColor(), idx);

            // Keep it visually selected while we wait for server approval
            _current.SetSelected(true);
            // We do NOT lock locally here.
            return;
        }

        // --- Local/offline behavior (original) ---
        if (_current == _locked && _locked != null) return;

        if (_locked != null)
        {
            _locked.Unlock();
            _locked.SetSelected(false);
        }

        _current.Lock();
        _locked = _current;

        int i2 = swatches.IndexOf(_current);
        onColorConfirmed?.Invoke(_current.GetFillColor(), i2);
        _current.SetSelected(true);

        UpdateConfirmInteractable();
    }

    public void ClearSelection()
    {
        _current = null;
        foreach (var s in swatches) if (s) s.SetSelected(false);
        UpdateConfirmInteractable();
    }

    public void ClearLock()
    {
        if (_locked != null) _locked.Unlock();
        _locked = null;
        UpdateConfirmInteractable();
    }

    void UpdateConfirmInteractable()
    {
        if (!confirmButton) return;
        confirmButton.interactable = (_current != null && _current != _locked);
    }

    // --- Helpers used by the multiplayer binder ---

    /// <summary>Called when the server has approved a lock for *this* player.</summary>
    public void SetLockedFromNetwork(int index)
    {
        if (index < 0 || index >= swatches.Count) return;
        var s = swatches[index];

        if (_locked != null && _locked != s)
        {
            _locked.Unlock();
            _locked.SetSelected(false);
        }

        // Apply the same visuals as local lock, but as a result of server approval
        s.Lock();
        s.SetSelected(true);
        _locked = s;

        UpdateConfirmInteractable();
    }

    /// <summary>Called for every swatch to reflect global lock state (any player).</summary>
    public void SetSwatchLockedState(int index, bool locked)
    {
        if (index < 0 || index >= swatches.Count) return;
        var s = swatches[index];
        if (!s) return;

        if (locked && !s.IsLocked) s.Lock();
        if (!locked && s.IsLocked) s.Unlock();

        // If the one we thought we owned got unlocked (e.g., server cleanup),
        // ensure our local pointer is cleared.
        if (!locked && _locked == s) _locked = null;

        UpdateConfirmInteractable();
    }

    // Used by ConfirmButtonNeonHover to color the glow
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
