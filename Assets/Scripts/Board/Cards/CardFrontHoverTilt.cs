using UnityEngine;

[DisallowMultipleComponent]
public class FrontCardHover3D : MonoBehaviour
{
    [Header("Targets (auto if left blank)")]
    public RectTransform flipCenter;
    public Canvas worldCanvas;

    [Header("Hover Feel")]
    [Range(1f, 1.2f)] public float hoverScale = 1.04f;
    [Range(0f, 25f)] public float maxTiltX = 8f;
    [Range(0f, 25f)] public float maxTiltY = 8f;
    [Range(0f, 10f)] public float maxRollZ = 2f;
    [Range(1f, 30f)] public float followSpeed = 12f;
    [Range(1f, 30f)] public float scaleSpeed = 12f;
    public bool useUnscaledTime = true;

    [HideInInspector] public bool isFrontFacing = false;
    [HideInInspector] public bool isFlipping = false;

    RectTransform _rt;
    Camera _uiCam;
    Vector3 _baseScale = Vector3.one;
    Vector3 _baseEuler;
    bool _wasFront;

    Vector3 _targetEuler;
    Vector3 _currentEuler;
    Vector3 _targetScale;
    Vector3 _currentScale;

    void Awake()
    {
        _rt = transform as RectTransform;
        if (!worldCanvas) worldCanvas = GetComponentInParent<Canvas>();
        if (!flipCenter)
        {
            var t = transform.Find("FlipCenter");
            if (t) flipCenter = t as RectTransform;
        }
    }

    void OnEnable()
    {
        CacheBase();
        SnapNeutral();
    }

    void CacheBase()
    {
        if (!flipCenter) return;
        _baseScale = flipCenter.localScale;
        _baseEuler = flipCenter.localEulerAngles;
        _currentEuler = _baseEuler;
        _currentScale = _baseScale;
        _targetEuler = _baseEuler;
        _targetScale = _baseScale;
    }

    void SnapNeutral()
    {
        if (!flipCenter) return;
        _targetEuler = _baseEuler;
        _targetScale = _baseScale;
        _currentEuler = _baseEuler;
        _currentScale = _baseScale;
        ApplyNow();
    }

    void LateUpdate()
    {
        if (!flipCenter) return;
        if (isFlipping) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        _uiCam = worldCanvas && worldCanvas.renderMode == RenderMode.WorldSpace
               ? worldCanvas.worldCamera
               : Camera.main;

        if (_wasFront != isFrontFacing)
        {
            _wasFront = isFrontFacing;
            CacheBase();
        }

        if (!isFrontFacing || isFlipping)
        {
            _targetEuler = _baseEuler;
            _targetScale = _baseScale;
        }
        else
        {
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(flipCenter, Input.mousePosition, _uiCam);

            if (!inside)
            {
                _targetEuler = _baseEuler;
                _targetScale = _baseScale;
            }
            else
            {
                Vector2 lp;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(flipCenter, Input.mousePosition, _uiCam, out lp);

                var r = flipCenter.rect;
                float nx = Mathf.Clamp(lp.x / (Mathf.Max(1e-4f, r.width) * 0.5f), -1f, 1f);
                float ny = Mathf.Clamp(lp.y / (Mathf.Max(1e-4f, r.height) * 0.5f), -1f, 1f);

                float tiltX = -ny * maxTiltX;
                float tiltY = nx * maxTiltY;
                float rollZ = -nx * maxRollZ * ny;

                _targetEuler = _baseEuler + new Vector3(tiltX, tiltY, rollZ);
                _targetScale = _baseScale * hoverScale;
            }
        }

        float rotAlpha = 1f - Mathf.Exp(-followSpeed * dt);
        _currentEuler = new Vector3(
            Mathf.LerpAngle(_currentEuler.x, _targetEuler.x, rotAlpha),
            Mathf.LerpAngle(_currentEuler.y, _targetEuler.y, rotAlpha),
            Mathf.LerpAngle(_currentEuler.z, _targetEuler.z, rotAlpha)
        );

        float sclAlpha = 1f - Mathf.Exp(-scaleSpeed * dt);
        _currentScale = Vector3.Lerp(_currentScale, _targetScale, sclAlpha);

        ApplyNow();
    }

    void ApplyNow()
    {
        flipCenter.localEulerAngles = _currentEuler;
        flipCenter.localScale = _currentScale;
    }

    public void SetFrontFacing(bool isFront) { isFrontFacing = isFront; }
    public void SetFlipping(bool flipping) { isFlipping = flipping; }

    public void SetFlipCenter(RectTransform rig)
    {
        flipCenter = rig;
        CacheBase();

        if (!isFlipping)
            SnapNeutral();
    }

}
