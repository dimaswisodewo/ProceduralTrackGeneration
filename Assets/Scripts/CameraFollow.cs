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
            currentYaw = target.eulerAngles.y;
            isInitialized = true;
        }
    }

    private void LateUpdate() {
        if (target == null) return;

        // If Rigidbody wasn't found at start, try to cache it
        if (targetRigidbody == null) {
            targetRigidbody = target.GetComponentInParent<Rigidbody>();
        }

        // Initialize yaw if not done yet
        if (!isInitialized) {
            currentYaw = target.eulerAngles.y;
            isInitialized = true;
        }

        // Cinematic Orbit Camera on GameOver / Victory State
        if (PackageDeliverySystem.Instance != null && PackageDeliverySystem.Instance.currentState == PackageDeliverySystem.DeliveryState.GameOver) {
            currentYaw += 15f * Time.deltaTime; // Rotates slowly
            Quaternion orbitRotation = Quaternion.Euler(15f, currentYaw, 0f);
            Vector3 targetCameraPosition = target.position - (orbitRotation * Vector3.forward * (distance * 0.8f));
            targetCameraPosition.y = target.position.y + (height * 0.8f);

            transform.position = targetCameraPosition;
            transform.LookAt(target.position + lookAtOffset);
            return;
        }

        // 1. Calculate Target Position & Rotation
        float targetYawAngle = target.eulerAngles.y;

        // Lerp current yaw towards target's heading yaw
        currentYaw = Mathf.LerpAngle(currentYaw, targetYawAngle, rotationDamping * Time.deltaTime);

        // Position the camera directly behind the target using the smoothed yaw
        Quaternion currentRotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 targetPosition = target.position - (currentRotation * Vector3.forward * distance);
        targetPosition.y = target.position.y + height;

        // 2. Smoothly Move Camera Position with shake
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionDamping * Time.deltaTime) + shakeOffset;

        // 3. Look at Target with offset and apply rotational shake
        Vector3 lookAtTarget = target.position + lookAtOffset;
        transform.LookAt(lookAtTarget);
        transform.rotation *= Quaternion.Euler(shakeRotationOffset);

        // 4. Handle Speed-based FOV Zooming with offsets
        if (cam != null) {
            float targetFOV = minFOV;
            if (enableSpeedFOV) {
                float speed = targetRigidbody != null ? targetRigidbody.linearVelocity.magnitude : 0f;
                float speedRatio = Mathf.Clamp01(speed / maxSpeedForFOV);
                targetFOV = Mathf.Lerp(minFOV, maxFOV, speedRatio);
            }
            cam.fieldOfView = targetFOV + boostFOVOffset + shockZoomOffset;
        }
    }

    private void OnDestroy() {
        if (shakeTween != null) shakeTween.Kill();
        if (shakeRotTween != null) shakeRotTween.Kill();
        if (boostFOVTween != null) boostFOVTween.Kill();
        if (shockZoomTween != null) shockZoomTween.Kill();
    }
}
