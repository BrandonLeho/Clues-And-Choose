using UnityEngine;

public class EnableAllCellHoversAfterFlyIn : MonoBehaviour
{
    [SerializeField] Transform gridRoot;
    [SerializeField] CardStackFlyInAnimator flyIn;

    void Awake()
    {
        if (!gridRoot) gridRoot = transform;
        ToggleAll(false);
        if (flyIn) flyIn.OnSequenceFinished.AddListener(EnableAll);
    }

    void OnDestroy()
    {
        if (flyIn) flyIn.OnSequenceFinished.RemoveListener(EnableAll);
    }

    void EnableAll() => ToggleAll(true);

    void ToggleAll(bool enabled)
    {
        var hovers = gridRoot.GetComponentsInChildren<GridCellHoverWithCoords>(true);
        for (int i = 0; i < hovers.Length; i++) hovers[i].enabled = enabled;
    }
}
