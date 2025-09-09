using UnityEngine;
using UnityEngine.EventSystems;

public class BackgroundDeselect : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] SelectionController picker;

    void Awake()
    {
        if (!picker) picker = FindFirstObjectByType<SelectionController>(FindObjectsInactive.Include);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!picker) return;

        if (picker.HasPendingSelection())
            picker.CancelCurrent();
    }
}
