using UnityEngine;

public class CoinHoverOffset : MonoBehaviour
{
    public Transform offsetTarget;

    Vector3 _baseLocal;
    bool _ready;

    void Awake()
    {
        if (!offsetTarget) offsetTarget = GuessOffsetTarget(transform);
        if (offsetTarget)
        {
            _baseLocal = offsetTarget.localPosition;
            _ready = true;
        }
    }

    Transform GuessOffsetTarget(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.GetComponent<SpriteRenderer>() || c.GetComponent<Renderer>())
                return c;
        }
        return root.childCount > 0 ? root.GetChild(0) : null;
    }

    public void SetWorldLift(float worldY)
    {
        if (!_ready) return;
        var parent = offsetTarget.parent ? offsetTarget.parent : transform;
        var local = parent.InverseTransformVector(new Vector3(0f, worldY, 0f));
        offsetTarget.localPosition = _baseLocal + local;
    }

    public void ResetOffset()
    {
        if (!_ready) return;
        offsetTarget.localPosition = _baseLocal;
    }

    void OnDisable() { if (_ready) ResetOffset(); }
}
