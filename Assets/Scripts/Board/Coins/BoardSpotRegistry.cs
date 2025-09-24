using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public class BoardSpotsRegistry : MonoBehaviour
{
    public static BoardSpotsRegistry Instance { get; private set; }

    public Transform root;

    ValidDropSpot[] _spotsByIndex;

    void Awake()
    {
        Instance = this;

        var all = (root ? root.GetComponentsInChildren<ValidDropSpot>(true)
                        : FindObjectsByType<ValidDropSpot>(FindObjectsSortMode.None)).ToList();

        all = all.OrderByDescending(s => s.transform.localPosition.y)
                 .ThenBy(s => s.transform.localPosition.x).ToList();

        _spotsByIndex = all.ToArray();
        for (int i = 0; i < _spotsByIndex.Length; i++)
        {
            _spotsByIndex[i].spotIndex = i;
        }
    }

    public ValidDropSpot Get(int spotIndex)
    {
        if (_spotsByIndex == null || spotIndex < 0 || spotIndex >= _spotsByIndex.Length) return null;
        return _spotsByIndex[spotIndex];
    }

    public int Count => _spotsByIndex?.Length ?? 0;

    public int FindNearestUsableIndex(Vector2 worldPoint)
    {
        float best = float.PositiveInfinity; int bestIdx = -1;
        for (int i = 0; i < _spotsByIndex.Length; i++)
        {
            var s = _spotsByIndex[i];
            if (!s || !s.enabledForPlacement) continue;
            if (!s.ContainsPoint(worldPoint)) continue;
            float d = Vector2.Distance(worldPoint, (Vector2)s.GetCenterWorld());
            if (d < best) { best = d; bestIdx = i; }
        }
        return bestIdx;
    }
}
