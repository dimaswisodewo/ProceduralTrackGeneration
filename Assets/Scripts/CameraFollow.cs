using UnityEngine;

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

    [Header("Camera Shake Settings")]
    private float shakeDuration = 0f;
    private float shakeMagnitude = 0.1f;
    private Vector3 shakeOffset = Vector3.zero;

    public void TriggerShake(float duration, float magnitude) {
        shakeDuration = duration;
        shakeMagnitude = magnitude;
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

        // 1. Calculate Target Position & Rotation
        float targetYawAngle = target.eulerAngles.y;

        // Lerp current yaw towards target's heading yaw
        currentYaw = Mathf.LerpAngle(currentYaw, targetYawAngle, rotationDamping * Time.deltaTime);

        // Position the camera directly behind the target using the smoothed yaw
        Quaternion currentRotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 targetPosition = target.position - (currentRotation * Vector3.forward * distance);
        targetPosition.y = target.position.y + height;

        // 1b. Handle Camera Shake
        if (shakeDuration > 0f) {
            shakeOffset = Random.insideUnitSphere * shakeMagnitude;
            shakeDuration -= Time.deltaTime;
        } else {
            shakeOffset = Vector3.zero;
        }

        // 2. Smoothly Move Camera Position
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionDamping * Time.deltaTime) + shakeOffset;

        // 3. Look at Target with offset
        Vector3 lookAtTarget = target.position + lookAtOffset;
        transform.LookAt(lookAtTarget);

        // 4. Handle Speed-based FOV Zooming
        if (enableSpeedFOV && cam != null) {
            float speed = targetRigidbody != null ? targetRigidbody.linearVelocity.magnitude : 0f;
            float speedRatio = Mathf.Clamp01(speed / maxSpeedForFOV);
            cam.fieldOfView = Mathf.Lerp(minFOV, maxFOV, speedRatio);
        }
    }
}
