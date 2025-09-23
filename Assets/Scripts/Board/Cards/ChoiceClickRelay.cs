using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ChoiceGridCoord))]
public class ChoiceClickRelay : MonoBehaviour, IPointerClickHandler
{
    public CardChoiceSelectController controller;

    [HideInInspector] public ChoiceGridCoord coord;
    [HideInInspector] public Image img;
    [HideInInspector] public CanvasGroup cg;

    void Awake()
    {
        coord = GetComponent<ChoiceGridCoord>();
        img = GetComponentInChildren<Image>(true);
        cg = gameObject.GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!controller || controller.HasLockedSelection) return;
        var c = img ? img.color : Color.white;
        controller.Select(this, coord.col, coord.row, c);
    }
}
