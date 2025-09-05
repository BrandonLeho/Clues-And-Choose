using UnityEngine;
using UnityEngine.EventSystems;

public class ChangeCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private CursorControllerModule.ModeOfCursor modeOfCursor;

    public void OnPointerEnter(PointerEventData eventData)
    {
        CursorControllerModule.Instance.SetToMode(modeOfCursor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }

    private void OnDisable()
    {
        if (CursorControllerModule.Instance != null)
            CursorControllerModule.Instance.SetToMode(CursorControllerModule.ModeOfCursor.Default);
    }
}
