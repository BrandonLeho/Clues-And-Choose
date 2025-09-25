using UnityEngine;

public class ArrowLagAnimator : MonoBehaviour
{
    [Header("References (local)")]
    public Transform tip;
    public float tipRestDistance = 1.0f;

    [Header("Lag Tuning (local)")]
    public float positionLagTime = 0.08f;
    public float rotationLagTime = 0.08f;
    public float bendAmplitude = 0.06f;
    public float maxBendOffset = 0.15f;

    [Header("Damping")]
    public float overshootDamping = 0.9f;

    Vector3 _tipVel;
    float _tipAngVel;
    Vector3 _lastRootLocalPos;
    Vector3 _smoothedBend;

    void Reset()
    {
        if (!tip)
        {
            var found = transform.Find("Tip");
            if (found) tip = found;
        }

        if (tip)
        {
            Vector3 lp = tip.localPosition;
            tipRestDistance = lp.y != 0f ? Mathf.Abs(lp.y) : tipRestDistance;
        }
    }

    void OnEnable()
    {
        _lastRootLocalPos = transform.localPosition;
        _smoothedBend = Vector3.zero;
        _tipVel = Vector3.zero;
        _tipAngVel = 0f;

        if (tip)
        {
            tip.localPosition = Vector3.up * tipRestDistance;
            tip.localRotation = Quaternion.identity;
        }
    }

    void LateUpdate()
    {
        if (!tip) return;

        Vector3 desiredTipLocalPos = Vector3.up * tipRestDistance;

        Vector3 rootDeltaLocal = transform.localPosition - _lastRootLocalPos;
        _lastRootLocalPos = transform.localPosition;

        Vector3 desiredBend = Vector3.zero;
        if (rootDeltaLocal.sqrMagnitude > 0f)
        {
            Vector3 localPlanarDelta = new Vector3(rootDeltaLocal.x, rootDeltaLocal.y, 0f);
            Vector3 oppose = -localPlanarDelta;
            float mag = oppose.magnitude;

            if (mag > 1e-4f)
            {
                Vector3 dir = oppose / mag;
                float amt = Mathf.Min(mag * bendAmplitude, maxBendOffset);
                desiredBend = dir * amt;
            }
        }

        _smoothedBend = Vector3.Lerp(_smoothedBend, desiredBend, 1f - Mathf.Pow(overshootDamping, Time.unscaledDeltaTime * 60f));

        Vector3 targetLocalPos = desiredTipLocalPos + _smoothedBend;
        tip.localPosition = Vector3.SmoothDamp(tip.localPosition, targetLocalPos, ref _tipVel, Mathf.Max(0.0001f, positionLagTime), Mathf.Infinity, Time.unscaledDeltaTime);

        float desiredAngleZ = 0f;
        float currentZ = tip.localEulerAngles.z;
        float newZ = Mathf.SmoothDampAngle(currentZ, desiredAngleZ, ref _tipAngVel, Mathf.Max(0.0001f, rotationLagTime), Mathf.Infinity, Time.unscaledDeltaTime);
        tip.localRotation = Quaternion.Euler(0f, 0f, newZ);
    }
}
