using UnityEngine;
using DG.Tweening;

public class CameraFollow : MonoBehaviour {
    [Header("Target")]
    public Transform target;

    [Header("Offset Settings")]
    public float distance = 6f;       // Distance behind target
    public float height = 2.5f;       // Height above target
    public Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);

    [Header("Damping (Smoothness)")]
    public float positionDamping = 5f;
    public float rotationDamping = 3f;

    [Header("Speed Effects")]
    public bool enableSpeedFOV = true;
    public float minFOV = 60f;
    public float maxFOV = 78f;
    public float maxSpeedForFOV = 25f; // Speed at which FOV is maximized (m/s)

    public static CameraFollow Instance { get; private set; }

    private Rigidbody targetRigidbody;
    private Camera cam;
    private float currentYaw;
    private bool isInitialized = false;
    private Vector3 currentFollowPosition;
    private Vector3 targetLocalPosition = Vector3.zero;
    private Quaternion targetLocalRotation = Quaternion.identity;

    [Header("Camera Shake Settings (DOTween)")]
    private Vector3 shakeOffset = Vector3.zero;
    private Vector3 shakeRotationOffset = Vector3.zero;
    private Tween shakeTween;
    private Tween shakeRotTween;

    [Header("Camera FOV Effects (DOTween)")]
    private float boostFOVOffset = 0f;
    private Tween boostFOVTween;
    private float shockZoomOffset = 0f;
    private Tween shockZoomTween;

    public void TriggerShake(float duration, float magnitude) {
        if (shakeTween != null) shakeTween.Kill();
        if (shakeRotTween != null) shakeRotTween.Kill();

        shakeOffset = Vector3.zero;
        shakeRotationOffset = Vector3.zero;

        // Positional screen shake
        shakeTween = DOTween.Shake(() => shakeOffset, x => shakeOffset = x, duration, magnitude, 20, 90f, true)
            .OnComplete(() => shakeOffset = Vector3.zero);

        // Rotational screen shake (multiplied for angles)
        float rotStrength = magnitude * 25f;
        shakeRotTween = DOTween.Shake(() => shakeRotationOffset, x => shakeRotationOffset = x, duration, new Vector3(rotStrength, rotStrength, rotStrength * 1.5f), 20, 90f, true)
            .OnComplete(() => shakeRotationOffset = Vector3.zero);
    }

    public void TriggerBoostFOV(float duration, float targetOffset) {
        if (boostFOVTween != null) boostFOVTween.Kill();
        boostFOVOffset = targetOffset;
        boostFOVTween = DOTween.To(() => boostFOVOffset, x => boostFOVOffset = x, 0f, duration)
            .SetEase(Ease.OutQuad);
    }

    public void TriggerShockZoom(float duration, float fovChange) {
        if (shockZoomTween != null) shockZoomTween.Kill();
        shockZoomOffset = fovChange;
        shockZoomTween = DOTween.To(() => shockZoomOffset, x => shockZoomOffset = x, 0f, duration)
            .SetEase(Ease.OutElastic);
    }

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        cam = GetComponent<Camera>();
        if (target != null) {
            targetRigidbody = target.GetComponentInParent<Rigidbody>();
            InitializeTargetTracking();
        }
    }

    private void LateUpdate() {
        if (target == null) return;

        // Ensure target rigidbody is cached if not found at start
        if (targetRigidbody == null) {
            targetRigidbody = target.GetComponentInParent<Rigidbody>();
        }

        if (!isInitialized) {
            InitializeTargetTracking();
        }

        // Cinematic Orbit Camera on GameOver / Victory State
        if (PackageDeliverySystem.Instance != null && PackageDeliverySystem.Instance.currentState == PackageDeliverySystem.DeliveryState.GameOver) {
            HandleGameOverOrbit();
            return;
        }

        // 1. Calculate Target Position & Rotation (interpolated for Rigidbody/child transforms)
        Vector3 targetPos = GetInterpolatedTargetPosition();
        float targetYawAngle = GetInterpolatedTargetYaw();

        // Lerp current yaw towards target's heading yaw (frame-rate independent)
        float rotT = 1f - Mathf.Exp(-rotationDamping * Time.deltaTime);
        currentYaw = Mathf.LerpAngle(currentYaw, targetYawAngle, rotT);

        // Position the camera directly behind the target using the smoothed yaw
        Quaternion currentRotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 targetPosition = targetPos - (currentRotation * Vector3.forward * distance);
        targetPosition.y = targetPos.y + height;

        // 2. Smoothly Move Camera Position without contaminating follow position with shakeOffset (frame-rate independent)
        float posT = 1f - Mathf.Exp(-positionDamping * Time.deltaTime);
        currentFollowPosition = Vector3.Lerp(currentFollowPosition, targetPosition, posT);
        transform.position = currentFollowPosition + shakeOffset;

        // 3. Look at Target with offset and apply rotational shake
        Vector3 lookAtTarget = targetPos + lookAtOffset;
        transform.LookAt(lookAtTarget);
        transform.rotation *= Quaternion.Euler(shakeRotationOffset);

        // 4. Handle Speed-based FOV Zooming with offsets
        UpdateCameraFOV();
    }

    private void InitializeTargetTracking() {
        if (targetRigidbody != null && target != targetRigidbody.transform) {
            targetLocalPosition = targetRigidbody.transform.InverseTransformPoint(target.position);
            targetLocalRotation = Quaternion.Inverse(targetRigidbody.transform.rotation) * target.rotation;
        }
        currentYaw = target.eulerAngles.y;
        currentFollowPosition = transform.position;
        isInitialized = true;
    }

    private Vector3 GetInterpolatedTargetPosition() {
        if (targetRigidbody != null) {
            return (target != targetRigidbody.transform)
                ? targetRigidbody.transform.TransformPoint(targetLocalPosition)
                : targetRigidbody.transform.position;
        }
        return target.position;
    }

    private float GetInterpolatedTargetYaw() {
        if (targetRigidbody != null) {
            return (target != targetRigidbody.transform)
                ? (targetRigidbody.transform.rotation * targetLocalRotation).eulerAngles.y
                : targetRigidbody.transform.eulerAngles.y;
        }
        return target.eulerAngles.y;
    }

    private void HandleGameOverOrbit() {
        currentYaw += 15f * Time.deltaTime; // Rotates slowly
        Quaternion orbitRotation = Quaternion.Euler(15f, currentYaw, 0f);
        
        Vector3 orbitTargetPos = GetInterpolatedTargetPosition();
        Vector3 targetCameraPosition = orbitTargetPos - (orbitRotation * Vector3.forward * (distance * 0.8f));
        targetCameraPosition.y = orbitTargetPos.y + (height * 0.8f);

        transform.position = targetCameraPosition;
        currentFollowPosition = targetCameraPosition;
        transform.LookAt(orbitTargetPos + lookAtOffset);
    }

    private void UpdateCameraFOV() {
        if (cam == null) return;

        float targetFOV = minFOV;
        if (enableSpeedFOV) {
            float speed = targetRigidbody != null ? targetRigidbody.linearVelocity.magnitude : 0f;
            float speedRatio = Mathf.Clamp01(speed / maxSpeedForFOV);
            targetFOV = Mathf.Lerp(minFOV, maxFOV, speedRatio);
        }
        cam.fieldOfView = targetFOV + boostFOVOffset + shockZoomOffset;
    }

    private void OnDestroy() {
        if (shakeTween != null) shakeTween.Kill();
        if (shakeRotTween != null) shakeRotTween.Kill();
        if (boostFOVTween != null) boostFOVTween.Kill();
        if (shockZoomTween != null) shockZoomTween.Kill();
    }
}
