using UnityEngine;
using UnityEngine.EventSystems;

public class ChangeCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private CursorControllerModule.ModeOfCursor modeOfCursor;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CursorControllerModule.Instance.IsLocked)
            CursorControllerModule.Instance.SetToMode(modeOfCursor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!CursorControllerModule.Instance.IsLocked)
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CursorControllerModule.Instance.IsLocked)
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }

    void OnDisable()
    {
        if (CursorControllerModule.Instance != null && !CursorControllerModule.Instance.IsLocked)
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }
}
