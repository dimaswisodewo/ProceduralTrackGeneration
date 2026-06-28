using UnityEngine;
using DG.Tweening;

public class CameraFollow : MonoBehaviour {
    [Header("Target")]
    public Transform target;

    [Header("Offset Settings")]
    public float distance = 6f;       // Distance behind target
    public float height = 12f;       // Height above target
    public Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);

    public enum CameraPOV {
        Top,
        Behind
    }

    [Header("POV Settings")]
    public CameraPOV currentPOV = CameraPOV.Top;

    [Tooltip("Top POV configurations")]
    public float topDistance = 6f;
    public float topHeight = 12f;
    public Vector3 topLookAtOffset = new Vector3(0f, 1f, 0f);

    [Tooltip("Behind POV configurations")]
    public float behindDistance = 3f;
    public float behindHeight = 1f;
    public Vector3 behindLookAtOffset = new Vector3(0f, 1f, 0f);


    [Header("Damping (Smoothness)")]
    public float positionDamping = 5f;
    public float rotationDamping = 5f;

    [Header("Speed Effects")]
    public bool enableSpeedFOV = true;
    public float minFOV = 60f;
    public float maxFOV = 78f;
    public float maxSpeedForFOV = 25f; // Speed at which FOV is maximized (m/s)

    [Header("Comfort & Motion Sickness Settings")]
    [Tooltip("If true, the Top POV camera will use comfort settings (smooth damping, no speed FOV, reduced screen shake).")]
    public bool topCameraComfortMode = true;

    [Tooltip("If true, the Behind POV camera will use comfort settings instead of standard follow behavior.")]
    public bool behindCameraComfortMode = false;

    [Tooltip("If true, locks the camera's yaw rotation (no spinning when car turns/spins) to prevent motion sickness.")]
    public bool lockYaw = false;

    [Tooltip("Smooth position damping used in Comfort Mode.")]
    public float comfortPositionDamping = 3.5f;

    [Tooltip("Smooth rotation damping used in Comfort Mode.")]
    public float comfortRotationDamping = 4.0f;

    private float initialTargetYaw = 0f;

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

    private void ApplyPOV(CameraPOV pov) {
        currentPOV = pov;
        switch (pov) {
            case CameraPOV.Top:
                distance = topDistance;
                height = topHeight;
                lookAtOffset = topLookAtOffset;
                break;
            case CameraPOV.Behind:
                distance = behindDistance;
                height = behindHeight;
                lookAtOffset = behindLookAtOffset;
                break;

        }
    }

    private void Start() {
        cam = GetComponent<Camera>();
        ApplyPOV(currentPOV);
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

        if (CarInputManager.Instance != null) {
            if (CarInputManager.Instance.ToggleCameraPressed) {
                CameraPOV nextPOV;
                switch (currentPOV) {
                    case CameraPOV.Top:
                        nextPOV = CameraPOV.Behind;
                        break;
                    case CameraPOV.Behind:
                    default:
                        nextPOV = CameraPOV.Top;
                        break;
                }
                ApplyPOV(nextPOV);
            } else if (CarInputManager.Instance.TopCameraPressed) {
                ApplyPOV(CameraPOV.Top);
            } else if (CarInputManager.Instance.BehindCameraPressed) {
                ApplyPOV(CameraPOV.Behind);
            }
        }

        // Check keyboard hotkeys for toggling yaw lock
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null) {
            if (keyboard.vKey.wasPressedThisFrame) {
                ToggleLockYaw();
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.V)) {
            ToggleLockYaw();
        }
#endif

        // Cinematic Orbit Camera on GameOver / Victory State
        if (PackageDeliverySystem.Instance != null && PackageDeliverySystem.Instance.currentState == PackageDeliverySystem.DeliveryState.GameOver) {
            HandleGameOverOrbit();
            return;
        }

        // 1. Calculate Target Position & Rotation (interpolated for Rigidbody/child transforms)
        Vector3 targetPos = GetInterpolatedTargetPosition();
        float targetYawAngle = GetInterpolatedTargetYaw();

        if (lockYaw) {
            targetYawAngle = initialTargetYaw;
        } else {
            // If comfort mode is active, align to velocity (movement direction) instead of target heading.
            // This keeps the camera orientation aligned with the path of travel and prevents dizzying screen swings during drifts/slides.
            if (IsComfortModeActive() && targetRigidbody != null) {
                Vector3 velocity = targetRigidbody.linearVelocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 1f) {
                    float rawVelocityYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                    
                    // Align to velocity but handle reversing to prevent instant 180-degree flips
                    Vector3 targetForward = target.forward;
                    targetForward.y = 0f;
                    targetForward.Normalize();
                    
                    float dot = Vector3.Dot(velocity.normalized, targetForward);
                    if (dot < 0f) {
                        targetYawAngle = rawVelocityYaw + 180f;
                    } else {
                        targetYawAngle = rawVelocityYaw;
                    }
                }
            }
        }

        // Lerp current yaw towards targetYawAngle (frame-rate independent)
        float rotDamp = IsComfortModeActive() ? comfortRotationDamping : rotationDamping;
        float rotT = 1f - Mathf.Exp(-rotDamp * Time.deltaTime);
        currentYaw = Mathf.LerpAngle(currentYaw, targetYawAngle, rotT);

        // Position the camera directly behind the target using the smoothed yaw
        Quaternion currentRotation = Quaternion.Euler(0f, currentYaw, 0f);
        
        Vector3 targetPosition = targetPos - (currentRotation * Vector3.forward * distance);
        targetPosition.y = targetPos.y + height;

        // 2. Smoothly Move Camera Position without contaminating follow position with shakeOffset (frame-rate independent)
        float posDamp = IsComfortModeActive() ? comfortPositionDamping : positionDamping;
        float posT = 1f - Mathf.Exp(-posDamp * Time.deltaTime);
        currentFollowPosition = Vector3.Lerp(currentFollowPosition, targetPosition, posT);
        
        Vector3 finalShakeOffset = IsComfortModeActive() ? (shakeOffset * 0.1f) : shakeOffset;
        transform.position = currentFollowPosition + finalShakeOffset;

        // 3. Look at Target/Apply rotation and apply rotational shake
        Vector3 lookAtTarget = targetPos + lookAtOffset;
        transform.LookAt(lookAtTarget);
        
        Vector3 finalShakeRot = IsComfortModeActive() ? (shakeRotationOffset * 0.1f) : shakeRotationOffset;
        transform.rotation *= Quaternion.Euler(finalShakeRot);

        // 4. Handle Speed-based FOV Zooming with offsets
        UpdateCameraFOV();
    }

    private void InitializeTargetTracking() {
        if (targetRigidbody != null && target != targetRigidbody.transform) {
            targetLocalPosition = targetRigidbody.transform.InverseTransformPoint(target.position);
            targetLocalRotation = Quaternion.Inverse(targetRigidbody.transform.rotation) * target.rotation;
        }
        currentYaw = target.eulerAngles.y;
        initialTargetYaw = target.eulerAngles.y;
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
        Vector3 targetCameraPosition = orbitTargetPos - (orbitRotation * Vector3.forward * (topDistance * 0.8f));
        targetCameraPosition.y = orbitTargetPos.y + (topHeight * 0.8f);

        transform.position = targetCameraPosition;
        currentFollowPosition = targetCameraPosition;
        transform.LookAt(orbitTargetPos + topLookAtOffset);
    }

    private void UpdateCameraFOV() {
        if (cam == null) return;

        float targetFOV = minFOV;
        bool comfort = IsComfortModeActive();
        if (enableSpeedFOV && !comfort) {
            float speed = targetRigidbody != null ? targetRigidbody.linearVelocity.magnitude : 0f;
            float speedRatio = Mathf.Clamp01(speed / maxSpeedForFOV);
            targetFOV = Mathf.Lerp(minFOV, maxFOV, speedRatio);
        }
        
        float boost = comfort ? 0f : boostFOVOffset;
        float shock = comfort ? 0f : shockZoomOffset;
        cam.fieldOfView = targetFOV + boost + shock;
    }

    public bool IsComfortModeActive() {
        return (currentPOV == CameraPOV.Top) ? topCameraComfortMode : behindCameraComfortMode;
    }

    public void ToggleLockYaw() {
        lockYaw = !lockYaw;
        ShowCameraModeNotification(lockYaw ? "Camera Rotation: LOCKED" : "Camera Rotation: FREE");
    }

    private void ShowCameraModeNotification(string message) {
        Debug.Log(message);
        if (UIManager.Instance != null) {
            UIManager.Instance.FlashNotificationText(message);
        }
    }

    public void ResetCamera() {
        if (target == null) return;

        targetRigidbody = target.GetComponentInParent<Rigidbody>();
        InitializeTargetTracking();

        Vector3 targetPos = GetInterpolatedTargetPosition();
        float targetYawAngle = GetInterpolatedTargetYaw();
        currentYaw = targetYawAngle;

        // Position the camera directly behind the target using the target yaw
        Quaternion currentRotation = Quaternion.Euler(15f, currentYaw, 0f);
        Vector3 targetPosition = targetPos - (currentRotation * Vector3.forward * distance);
        targetPosition.y = targetPos.y + height;

        transform.position = targetPosition;
        currentFollowPosition = targetPosition;
        
        Vector3 lookAtTarget = targetPos + lookAtOffset;
        transform.LookAt(lookAtTarget);
    }

    private void OnDestroy() {
        if (shakeTween != null) shakeTween.Kill();
        if (shakeRotTween != null) shakeRotTween.Kill();
        if (boostFOVTween != null) boostFOVTween.Kill();
        if (shockZoomTween != null) shockZoomTween.Kill();
    }
}
