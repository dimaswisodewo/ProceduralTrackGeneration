using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour {
    public enum DriveType {
        AllWheelDrive,
        RearWheelDrive,
        FrontWheelDrive
    }

    [System.Serializable]
    public struct Wheel {
        public WheelCollider collider;
        public Transform mesh;
        public bool isFront;
    }

    [Header("Drive Settings")]
    public DriveType driveType = DriveType.AllWheelDrive;
    public float motorTorque = 1000f;
    public float maxSpeed = 16f; // in meters per second (~58 km/h)
    public float brakeForce = 2000f;
    public float handbrakeForce = 4000f;

    [Header("Steering Settings")]
    public float maxSteerAngle = 25f;
    public float highSpeedSteerLimit = 30f; // Speed (m/s) at which steering angle is minimized
    public float minSteerRange = 0.3f;      // Steer angle scale at high speed (e.g. 30% of max steer)

    [Header("Physics & Stability")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.6f, 0.1f);
    public float downforceCoefficient = 120f;
    public float antiRollForce = 8000f;
    [Range(0f, 1f)]
    public float steerHelper = 0.6f; // 0 = no help (spin out/drift), 1 = full help (no sliding)

    private float lastRotationYaw;

    [Header("Drift Assist Settings")]
    [Range(0.1f, 1f)]
    public float handbrakeSidewaysStiffness = 0.4f; // Lower stiffness = easier to slide/drift
    [Range(0.1f, 1f)]
    public float handbrakeForwardStiffness = 0.6f;

    [Header("Drift Settings")]
    public float driftRearSidewaysStiffness = 0.35f;
    public float driftSteerLimitMultiplier = 1.3f;
    public float driftTorqueFactor = 1.25f;
    public float driftYawForce = 2.5f;
    public float driftSteerHelper = 0.15f;

    [Header("Drift Boost Settings")]
    public float driftBoostLevel1Time = 0.6f;
    public float driftBoostLevel2Time = 1.3f;
    public float driftBoostLevel1Force = 7f;
    public float driftBoostLevel2Force = 13f;
    public float driftBoostLevel1Duration = 0.5f;
    public float driftBoostLevel2Duration = 1.0f;
    public float driftBoostLevel1SpeedMult = 1.25f;
    public float driftBoostLevel2SpeedMult = 1.45f;

    [Header("Drift Visuals")]
    public float sparkEmitInterval = 0.06f;

    private bool isDrifting = false;
    private float driftChargeTime = 0f;
    private float boostTimeRemaining = 0f;
    private float currentBoostMultiplier = 1f;
    private float sparkEmitTimer = 0f;

    private Material driftSparkMat0;
    private Material driftSparkMat1;
    private Material driftSparkMat2;
    private Material redMaterialInstance;

    private WheelFrictionCurve originalFrontLeftSideways;
    private WheelFrictionCurve originalFrontRightSideways;
    private WheelFrictionCurve originalFrontLeftForward;
    private WheelFrictionCurve originalFrontRightForward;

    [Header("Wheels Setup")]
    public Wheel frontLeft;
    public Wheel frontRight;
    public Wheel rearLeft;
    public Wheel rearRight;

    private Rigidbody rb;
    private WheelFrictionCurve originalRearLeftSideways;
    private WheelFrictionCurve originalRearRightSideways;
    private WheelFrictionCurve originalRearLeftForward;
    private WheelFrictionCurve originalRearRightForward;

    [Header("Safe Position Tracker")]
    public float safePositionCheckInterval = 0.5f;
    private float safePositionTimer = 0f;
    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation;

    [Header("Bounce Settings")]
    public float bounceForceFactor = 0.6f;
    public float verticalBounceForce = 0.25f;
    public float minBounceSpeed = 3f;
    public float bounceCooldown = 0.3f;
    public float bounceStunDuration = 0.25f;

    [Header("Bounce Visual Effects")]
    public Transform carBodyVisual;
    public bool enableSquashAndStretch = true;
    public float squashDuration = 0.3f;
    public bool showBounceSparks = true;

    [Header("Object Pooling Settings")]
    public int maxSparkPoolSize = 80;
    private Queue<GameObject> sparkPool = new Queue<GameObject>();
    private Material[] poolMaterials;
    private Transform poolRoot;

    private float lastBounceTime = 0f;
    private float stunTimer = 0f;
    private Vector3 originalBodyScale;
    private Coroutine squashCoroutine;

    private void Start() {
        gameObject.tag = "Player";
        rb = GetComponent<Rigidbody>();
        
        // Lower the center of mass to prevent rolling over easily
        rb.centerOfMass = centerOfMassOffset;

        lastRotationYaw = transform.eulerAngles.y;

        // Store original wheel friction settings to restore them after handbraking/drifting
        if (frontLeft.collider && frontRight.collider) {
            originalFrontLeftSideways = frontLeft.collider.sidewaysFriction;
            originalFrontRightSideways = frontRight.collider.sidewaysFriction;
            originalFrontLeftForward = frontLeft.collider.forwardFriction;
            originalFrontRightForward = frontRight.collider.forwardFriction;
        }
        if (rearLeft.collider && rearRight.collider) {
            originalRearLeftSideways = rearLeft.collider.sidewaysFriction;
            originalRearRightSideways = rearRight.collider.sidewaysFriction;
            originalRearLeftForward = rearLeft.collider.forwardFriction;
            originalRearRightForward = rearRight.collider.forwardFriction;
        }

        // Create custom materials for drift sparks
        Shader sparkShader = FindSparkShader();

        driftSparkMat0 = new Material(sparkShader);
        driftSparkMat0.color = new Color(0.9f, 0.65f, 1f); // Pastel Purple
        
        driftSparkMat1 = new Material(sparkShader);
        driftSparkMat1.color = new Color(0.3f, 0.85f, 1f); // Pastel Cyan/Blue

        driftSparkMat2 = new Material(sparkShader);
        driftSparkMat2.color = new Color(1f, 0.4f, 0.4f); // Pastel Pink/Red

        // Initialize safe position tracking at spawn point
        lastSafePosition = transform.position;
        lastSafeRotation = transform.rotation;

        // Auto-detect car body visual child object if not assigned
        if (carBodyVisual == null) {
            carBodyVisual = transform.Find("Parent");
            if (carBodyVisual == null) {
                carBodyVisual = transform.Find("CarMesh");
            }
            if (carBodyVisual == null) {
                // Find first MeshRenderer under children that is not a wheel
                MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
                foreach (var r in renderers) {
                    if (r.gameObject != gameObject && !r.name.Contains("Mesh")) {
                        carBodyVisual = r.transform;
                        break;
                    }
                }
                if (carBodyVisual == null && renderers.Length > 0) {
                    carBodyVisual = renderers[0].transform;
                }
            }
        }

        if (carBodyVisual != null) {
            originalBodyScale = carBodyVisual.localScale;
        } else {
            originalBodyScale = Vector3.one;
        }

        // Make the car body red as requested using a single shared material instance
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        HashSet<Transform> wheelTransforms = new HashSet<Transform>();
        if (frontLeft.mesh != null) wheelTransforms.Add(frontLeft.mesh);
        if (frontRight.mesh != null) wheelTransforms.Add(frontRight.mesh);
        if (rearLeft.mesh != null) wheelTransforms.Add(rearLeft.mesh);
        if (rearRight.mesh != null) wheelTransforms.Add(rearRight.mesh);

        Color redColor = new Color(0.85f, 0.15f, 0.15f); // Clean, premium vibrant red
        foreach (var r in allRenderers) {
            // Check if this renderer or any of its parents is in the wheel list
            Transform t = r.transform;
            bool isWheel = false;
            while (t != null && t != transform) {
                if (wheelTransforms.Contains(t) || t.name.ToLower().Contains("wheel")) {
                    isWheel = true;
                    break;
                }
                t = t.parent;
            }
            if (!isWheel) {
                if (redMaterialInstance == null && r.sharedMaterial != null) {
                    redMaterialInstance = new Material(r.sharedMaterial);
                    redMaterialInstance.color = redColor;
                    if (redMaterialInstance.HasProperty("_BaseColor")) {
                        redMaterialInstance.SetColor("_BaseColor", redColor);
                    }
                }
                if (redMaterialInstance != null) {
                    r.sharedMaterial = redMaterialInstance;
                }
            }
        }

        // Initialize collision sparks object pool
        InitializeSparkPool();
    }

    public void SetInitialPosition(Vector3 position, Quaternion rotation) {
        transform.position = position;
        transform.rotation = rotation;
        lastSafePosition = position;
        lastSafeRotation = rotation;
        lastRotationYaw = rotation.eulerAngles.y;
        
        if (rb == null) {
            rb = GetComponent<Rigidbody>();
        }
        if (rb != null) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ResetWheelState(frontLeft);
        ResetWheelState(frontRight);
        ResetWheelState(rearLeft);
        ResetWheelState(rearRight);
    }

    private void FixedUpdate() {
        if (CarInputManager.Instance == null) return;

        float throttle = CarInputManager.Instance.Throttle;
        float steering = CarInputManager.Instance.Steering;
        float brake = CarInputManager.Instance.Brake;
        bool handbrake = CarInputManager.Instance.Handbrake;

        // Process stun control reduction
        if (stunTimer > 0f) {
            stunTimer -= Time.fixedDeltaTime;
            throttle *= 0.1f;    // Reduce engine torque significantly
            steering *= 0.2f;    // Reduce steering control
            brake = 0f;          // Disable standard braking to allow rebound physics
            handbrake = false;
        }

        bool driftInput = CarInputManager.Instance.Drift && (stunTimer <= 0f);

        // Check if grounded
        bool isGrounded = (frontLeft.collider && frontLeft.collider.isGrounded) ||
                           (frontRight.collider && frontRight.collider.isGrounded) ||
                           (rearLeft.collider && rearLeft.collider.isGrounded) ||
                           (rearRight.collider && rearRight.collider.isGrounded);

        float currentSpeedForDrift = rb.linearVelocity.magnitude;
        Vector3 localVelocityForDrift = transform.InverseTransformDirection(rb.linearVelocity);

        // Drift state logic: initiate drift if holding Shift, steering, grounded, and moving forward
        bool canStartDrift = isGrounded && driftInput && Mathf.Abs(steering) > 0.1f && localVelocityForDrift.z > 2f;
        if (isDrifting) {
            // Maintain drift as long as shift is held and speed is reasonable and we are on ground
            if (!driftInput || !isGrounded || localVelocityForDrift.z < 1.5f) {
                isDrifting = false;
            }
        } else {
            if (canStartDrift) {
                isDrifting = true;
            }
        }

        // Drift boost charging
        if (isDrifting) {
            driftChargeTime += Time.fixedDeltaTime;
            
            // Spawn drift sparks at interval
            sparkEmitTimer += Time.fixedDeltaTime;
            if (sparkEmitTimer >= sparkEmitInterval) {
                sparkEmitTimer = 0f;
                EmitDriftSparks();
            }
        } else {
            if (driftChargeTime > 0f) {
                // Exit drift: Trigger boost if charged enough
                if (driftChargeTime >= driftBoostLevel2Time) {
                    TriggerDriftBoost(2);
                } else if (driftChargeTime >= driftBoostLevel1Time) {
                    TriggerDriftBoost(1);
                }
                driftChargeTime = 0f;
            }
            sparkEmitTimer = 0f;
        }

        // Drift boost duration countdown
        if (boostTimeRemaining > 0f) {
            boostTimeRemaining -= Time.fixedDeltaTime;
            if (boostTimeRemaining <= 0f) {
                currentBoostMultiplier = 1f;
            }
        }

        // Apply helper yaw torque during drift
        if (isDrifting && isGrounded) {
            rb.AddTorque(transform.up * steering * driftYawForce, ForceMode.Acceleration);
        }

        // Calculate local forward speed to determine reverse gear behavior
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardSpeed = localVelocity.z;

        float targetMotorTorque = 0f;
        float targetBrakeTorque = 0f;

        float speedThreshold = 0.5f;

        if (forwardSpeed > speedThreshold) {
            // Moving forward: Throttle accelerates forward, Brake slows down
            targetMotorTorque = throttle;
            targetBrakeTorque = brake;
        } else if (forwardSpeed < -speedThreshold) {
            // Moving backward: Brake accelerates backward (reverse), Throttle slows down
            targetMotorTorque = -brake;
            targetBrakeTorque = throttle;
        } else {
            // Stationary / almost stopped: input determines direction
            if (throttle > 0.01f) {
                targetMotorTorque = throttle;
                targetBrakeTorque = 0f;
            } else if (brake > 0.01f) {
                targetMotorTorque = -brake;
                targetBrakeTorque = 0f;
            } else {
                targetMotorTorque = 0f;
                targetBrakeTorque = 0f;
            }
        }

        // 1. Motor & Acceleration
        ApplyMotorTorque(targetMotorTorque);

        // 2. Steering with Speed-Sensitivity
        ApplySteering(steering);

        // 3. Braking and Drift Assist
        ApplyBrakes(targetBrakeTorque, handbrake);

        // 4. Anti-Roll Stabilizer Bars
        ApplyAntiRollBars();

        // 5. Downforce
        ApplyDownforce();

        // 5b. Steer Helper / Yaw Stabilizer
        ApplySteerHelper();

        // 6. Safe Position Tracking
        TrackSafePosition();
    }

    private void Update() {
        // Handle Reset/Respawn Input
        if (CarInputManager.Instance != null && CarInputManager.Instance.ResetPressed) {
            RespawnAtLastSafePosition();
        }

        // Update visual mesh positions and rotations to match physical colliders
        UpdateWheelVisual(frontLeft);
        UpdateWheelVisual(frontRight);
        UpdateWheelVisual(rearLeft);
        UpdateWheelVisual(rearRight);
    }

    private void ApplyMotorTorque(float torqueInput) {
        // Calculate local forward speed
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardSpeed = localVelocity.z;

        float currentSpeed = Mathf.Abs(forwardSpeed);

        // We limit reverse speed to 40% of max forward speed
        float speedLimit = torqueInput < 0f ? (maxSpeed * 0.4f) : maxSpeed * currentBoostMultiplier;

        // Torque decreases to 0 as speed approaches speedLimit
        float speedRatio = currentSpeed / speedLimit;
        float torqueFactor = Mathf.Clamp01(1f - speedRatio);
        float torque = torqueInput * motorTorque * torqueFactor;

        if (isDrifting) {
            torque *= driftTorqueFactor;
        }

        // Cut power to the rear wheels if the handbrake is active to let them lock up
        bool isHandbraking = CarInputManager.Instance != null && CarInputManager.Instance.Handbrake;

        switch (driveType) {
            case DriveType.AllWheelDrive:
                frontLeft.collider.motorTorque = torque * 0.4f;
                frontRight.collider.motorTorque = torque * 0.4f;
                rearLeft.collider.motorTorque = isHandbraking ? 0f : (torque * 0.6f); // RWD bias for fun handling
                rearRight.collider.motorTorque = isHandbraking ? 0f : (torque * 0.6f);
                break;
            case DriveType.RearWheelDrive:
                frontLeft.collider.motorTorque = 0f;
                frontRight.collider.motorTorque = 0f;
                rearLeft.collider.motorTorque = isHandbraking ? 0f : (torque * 0.5f);
                rearRight.collider.motorTorque = isHandbraking ? 0f : (torque * 0.5f);
                break;
            case DriveType.FrontWheelDrive:
                frontLeft.collider.motorTorque = torque * 0.5f;
                frontRight.collider.motorTorque = torque * 0.5f;
                rearLeft.collider.motorTorque = 0f;
                rearRight.collider.motorTorque = 0f;
                break;
        }
    }

    private void ApplySteering(float steeringInput) {
        float currentSpeed = rb.linearVelocity.magnitude;

        // Scale down steering angle at higher speeds to prevent flipping and spin-outs
        float steerScale = Mathf.Lerp(1.0f, minSteerRange, currentSpeed / highSpeedSteerLimit);
        
        if (isDrifting) {
            steerScale *= driftSteerLimitMultiplier;
        }

        float steerAngle = steeringInput * maxSteerAngle * steerScale;

        frontLeft.collider.steerAngle = steerAngle;
        frontRight.collider.steerAngle = steerAngle;
    }

    private void ApplyBrakes(float brakeInput, bool handbrakeInput) {
        // Regular pedal braking
        float appliedBrake = brakeInput * brakeForce;

        if (handbrakeInput) {
            // Apply heavy braking to the rear wheels only
            rearLeft.collider.brakeTorque = handbrakeForce;
            rearRight.collider.brakeTorque = handbrakeForce;
            
            // Allow front wheels to roll/steer normally
            frontLeft.collider.brakeTorque = appliedBrake;
            frontRight.collider.brakeTorque = appliedBrake;
        } else {
            // Standard braking on all 4 wheels
            frontLeft.collider.brakeTorque = appliedBrake;
            frontRight.collider.brakeTorque = appliedBrake;
            rearLeft.collider.brakeTorque = appliedBrake;
            rearRight.collider.brakeTorque = appliedBrake;
        }

        // Dynamically update friction stiffness based on handbrake or drift
        UpdateWheelFrictions(handbrakeInput, isDrifting);
    }

    private void SetWheelFriction(WheelCollider wc, float forwardStiffness, float sidewaysStiffness) {
        if (!wc) return;
        WheelFrictionCurve fw = wc.forwardFriction;
        fw.stiffness = forwardStiffness;
        wc.forwardFriction = fw;

        WheelFrictionCurve sw = wc.sidewaysFriction;
        sw.stiffness = sidewaysStiffness;
        wc.sidewaysFriction = sw;
    }

    private void RestoreAllFriction() {
        if (frontLeft.collider) {
            frontLeft.collider.forwardFriction = originalFrontLeftForward;
            frontLeft.collider.sidewaysFriction = originalFrontLeftSideways;
        }
        if (frontRight.collider) {
            frontRight.collider.forwardFriction = originalFrontRightForward;
            frontRight.collider.sidewaysFriction = originalFrontRightSideways;
        }
        if (rearLeft.collider) {
            rearLeft.collider.forwardFriction = originalRearLeftForward;
            rearLeft.collider.sidewaysFriction = originalRearLeftSideways;
        }
        if (rearRight.collider) {
            rearRight.collider.forwardFriction = originalRearRightForward;
            rearRight.collider.sidewaysFriction = originalRearRightSideways;
        }
    }

    private void UpdateWheelFrictions(bool handbrakeActive, bool driftActive) {
        if (!rearLeft.collider || !rearRight.collider || !frontLeft.collider || !frontRight.collider) return;

        if (handbrakeActive) {
            SetWheelFriction(frontLeft.collider, 1f, 1f);
            SetWheelFriction(frontRight.collider, 1f, 1f);
            SetWheelFriction(rearLeft.collider, handbrakeForwardStiffness, handbrakeSidewaysStiffness);
            SetWheelFriction(rearRight.collider, handbrakeForwardStiffness, handbrakeSidewaysStiffness);
        } else if (driftActive) {
            // Front wheels keep original friction values
            SetWheelFriction(frontLeft.collider, originalFrontLeftForward.stiffness, originalFrontLeftSideways.stiffness);
            SetWheelFriction(frontRight.collider, originalFrontRightForward.stiffness, originalFrontRightSideways.stiffness);
            // Rear wheels get reduced sideways stiffness but maintain forward drive/grip
            SetWheelFriction(rearLeft.collider, originalRearLeftForward.stiffness, driftRearSidewaysStiffness);
            SetWheelFriction(rearRight.collider, originalRearRightForward.stiffness, driftRearSidewaysStiffness);
        } else {
            RestoreAllFriction();
        }
    }

    private void ApplyAntiRollBars() {
        if (frontLeft.collider && frontRight.collider) {
            ApplyAntiRollForceOnAxle(frontLeft.collider, frontRight.collider);
        }
        if (rearLeft.collider && rearRight.collider) {
            ApplyAntiRollForceOnAxle(rearLeft.collider, rearRight.collider);
        }
    }

    private void ApplyAntiRollForceOnAxle(WheelCollider leftWheel, WheelCollider rightWheel) {
        WheelHit hit;
        float travelL = 1.0f;
        float travelR = 1.0f;

        // Calculate left wheel suspension compression travel
        bool groundedL = leftWheel.GetGroundHit(out hit);
        if (groundedL) {
            travelL = (-leftWheel.transform.InverseTransformPoint(hit.point).y - leftWheel.radius) / leftWheel.suspensionDistance;
        }

        // Calculate right wheel suspension compression travel
        bool groundedR = rightWheel.GetGroundHit(out hit);
        if (groundedR) {
            travelR = (-rightWheel.transform.InverseTransformPoint(hit.point).y - rightWheel.radius) / rightWheel.suspensionDistance;
        }

        // The force is proportional to the difference in compression
        float force = (travelL - travelR) * antiRollForce;

        // Apply equal and opposite downward/upward force to the rigidbody
        if (groundedL) {
            rb.AddForceAtPosition(leftWheel.transform.up * -force, leftWheel.transform.position);
        }
        if (groundedR) {
            rb.AddForceAtPosition(rightWheel.transform.up * force, rightWheel.transform.position);
        }
    }

    private void ApplyDownforce() {
        // Apply custom downforce (proportional to speed) to keep the car glued to the track
        float velocityMag = rb.linearVelocity.magnitude;
        Vector3 force = -transform.up * downforceCoefficient * velocityMag;
        rb.AddForce(force);
    }

    private void UpdateWheelVisual(Wheel wheel) {
        if (!wheel.collider || !wheel.mesh) return;

        Vector3 pos;
        Quaternion rot;
        wheel.collider.GetWorldPose(out pos, out rot);

        wheel.mesh.position = pos;
        wheel.mesh.rotation = rot;
    }

    private void RespawnAtLastSafePosition() {
        // Teleport the Rigidbody
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        // Add a slight height offset to prevent clipping into the road surface
        transform.position = lastSafePosition + Vector3.up * 0.5f;
        transform.rotation = lastSafeRotation;

        // Reset steer helper tracker to avoid massive velocity changes on teleport
        lastRotationYaw = lastSafeRotation.eulerAngles.y;

        // Reset wheel colliders to prevent physics glitches upon teleport
        ResetWheelState(frontLeft);
        ResetWheelState(frontRight);
        ResetWheelState(rearLeft);
        ResetWheelState(rearRight);

        // Notify PackageDeliverySystem to restore/reset the package state
        if (PackageDeliverySystem.Instance != null) {
            PackageDeliverySystem.Instance.OnCarRespawn();
        }
    }

    private void ResetWheelState(Wheel wheel) {
        if (wheel.collider != null) {
            wheel.collider.motorTorque = 0f;
            wheel.collider.brakeTorque = 0f;
            wheel.collider.steerAngle = 0f;
        }
    }

    private void TrackSafePosition() {
        safePositionTimer += Time.fixedDeltaTime;
        if (safePositionTimer >= safePositionCheckInterval) {
            safePositionTimer = 0f;

            // 1. Check if the car is right-side up (up vector is mostly vertical)
            if (Vector3.Dot(transform.up, Vector3.up) > 0.7f) {
                // 2. Check if at least some wheels are grounded
                bool anyGrounded = (frontLeft.collider && frontLeft.collider.isGrounded) ||
                                  (frontRight.collider && frontRight.collider.isGrounded) ||
                                  (rearLeft.collider && rearLeft.collider.isGrounded) ||
                                  (rearRight.collider && rearRight.collider.isGrounded);

                if (anyGrounded) {
                    // 3. Raycast down to verify we are over a RoadPiece
                    RaycastHit hit;
                    Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
                    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 4f)) {
                        RoadPiece road = hit.collider.GetComponentInParent<RoadPiece>();
                        if (road != null) {
                            // Only update if moving at a reasonable speed, to avoid saving stuck positions
                            float speed = rb.linearVelocity.magnitude;
                            if (speed > 1f) {
                                lastSafePosition = transform.position;
                                // Keep the yaw rotation, but make roll/pitch flat so the car resets upright
                                Vector3 forward = transform.forward;
                                forward.y = 0f;
                                if (forward.sqrMagnitude > 0.001f) {
                                    lastSafeRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                                } else {
                                    lastSafeRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void ApplySteerHelper() {
        // Only apply steer helper when the car is grounded
        bool isGrounded = (frontLeft.collider && frontLeft.collider.isGrounded) ||
                           (frontRight.collider && frontRight.collider.isGrounded) ||
                           (rearLeft.collider && rearLeft.collider.isGrounded) ||
                           (rearRight.collider && rearRight.collider.isGrounded);
        if (!isGrounded) return;

        float currentYaw = transform.eulerAngles.y;
        float yawDiff = Mathf.DeltaAngle(lastRotationYaw, currentYaw);
        lastRotationYaw = currentYaw;

        // Disable/reduce stabilizer helper during handbraking or drifting to allow sliding
        float activeSteerHelper = steerHelper;
        if (CarInputManager.Instance != null && CarInputManager.Instance.Handbrake) {
            activeSteerHelper = 0f;
        } else if (isDrifting) {
            activeSteerHelper = driftSteerHelper;
        }

        // Correct velocity direction based on rotation change.
        // This makes the vehicle follow the steer/heading instead of spinning out.
        if (Mathf.Abs(yawDiff) < 10f && activeSteerHelper > 0.001f) {
            float turnAdjust = yawDiff * activeSteerHelper;
            Quaternion velRotation = Quaternion.AngleAxis(turnAdjust, Vector3.up);
            rb.linearVelocity = velRotation * rb.linearVelocity;
        }
    }

    private void OnCollisionEnter(Collision collision) {
        if (collision.contacts.Length == 0) return;

        // Ignore road/ground pieces
        if (collision.gameObject.GetComponentInParent<RoadPiece>() != null) return;

        Vector3 contactNormal = collision.contacts[0].normal;
        // Filter out floor/ceiling hits (we only bounce off walls/buildings)
        if (Mathf.Abs(contactNormal.y) > 0.6f) return;

        // Calculate collision impact speed
        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minBounceSpeed) return;

        // Check bounce cooldown
        if (Time.time < lastBounceTime + bounceCooldown) return;
        lastBounceTime = Time.time;

        // Rebound direction (flattened along Y axis to keep it horizontal)
        Vector3 bounceNormal = contactNormal;
        bounceNormal.y = 0f;
        bounceNormal.Normalize();

        // Apply rebound velocity impulse
        Vector3 reboundVel = bounceNormal * impactSpeed * bounceForceFactor;
        
        // Add vertical hop (upwards impulse) to make the bounce feel juicy
        reboundVel += Vector3.up * impactSpeed * verticalBounceForce;

        // Cancel out velocity moving directly into the obstacle
        Vector3 currentVel = rb.linearVelocity;
        float normalVel = Vector3.Dot(currentVel, bounceNormal);
        if (normalVel < 0f) {
            currentVel -= bounceNormal * normalVel;
        }
        rb.linearVelocity = currentVel + reboundVel;

        // Apply stun
        stunTimer = bounceStunDuration;

        // Trigger visual Squash & Stretch
        if (enableSquashAndStretch && carBodyVisual != null) {
            if (squashCoroutine != null) {
                StopCoroutine(squashCoroutine);
                carBodyVisual.localScale = originalBodyScale;
            }
            squashCoroutine = StartCoroutine(SquashAndStretchRoutine(contactNormal, impactSpeed));
        }

        // Spawn procedural Sparks
        if (showBounceSparks) {
            SpawnCollisionSparks(collision.contacts[0].point, contactNormal, impactSpeed);
        }

        // Trigger Camera Shake
        if (CameraFollow.Instance != null) {
            float shakeMagnitude = Mathf.Clamp(impactSpeed * 0.02f, 0.05f, 0.4f);
            float shakeDuration = Mathf.Clamp(impactSpeed * 0.015f, 0.1f, 0.35f);
            CameraFollow.Instance.TriggerShake(shakeDuration, shakeMagnitude);
        }
    }

    private System.Collections.IEnumerator SquashAndStretchRoutine(Vector3 contactNormal, float impactSpeed) {
        if (carBodyVisual == null) yield break;

        float maxSquashFactor = Mathf.Clamp(impactSpeed * 0.015f, 0.05f, 0.22f);
        float duration = squashDuration;
        float elapsed = 0f;

        float dotForward = Mathf.Abs(Vector3.Dot(contactNormal, transform.forward));
        Vector3 squashScale = originalBodyScale;
        Vector3 stretchScale = originalBodyScale;

        if (dotForward > 0.5f) {
            squashScale.z *= (1f - maxSquashFactor);
            squashScale.y *= (1f + maxSquashFactor * 0.5f);
            squashScale.x *= (1f + maxSquashFactor * 0.5f);
        } else {
            squashScale.x *= (1f - maxSquashFactor);
            squashScale.y *= (1f + maxSquashFactor * 0.5f);
            squashScale.z *= (1f + maxSquashFactor * 0.5f);
        }

        float squashTime = duration * 0.25f;
        while (elapsed < squashTime) {
            if (carBodyVisual == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / squashTime;
            carBodyVisual.localScale = Vector3.Lerp(originalBodyScale, squashScale, t);
            yield return null;
        }

        elapsed = 0f;
        float stretchTime = duration * 0.35f;
        if (dotForward > 0.5f) {
            stretchScale.z *= (1f + maxSquashFactor * 0.4f);
            stretchScale.y *= (1f - maxSquashFactor * 0.2f);
            stretchScale.x *= (1f - maxSquashFactor * 0.2f);
        } else {
            stretchScale.x *= (1f + maxSquashFactor * 0.4f);
            stretchScale.y *= (1f - maxSquashFactor * 0.2f);
            stretchScale.z *= (1f - maxSquashFactor * 0.2f);
        }

        while (elapsed < stretchTime) {
            if (carBodyVisual == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / stretchTime;
            carBodyVisual.localScale = Vector3.Lerp(squashScale, stretchScale, t);
            yield return null;
        }

        elapsed = 0f;
        float settleTime = duration * 0.4f;
        while (elapsed < settleTime) {
            if (carBodyVisual == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / settleTime;
            carBodyVisual.localScale = Vector3.Lerp(stretchScale, originalBodyScale, t);
            yield return null;
        }

        if (carBodyVisual != null) {
            carBodyVisual.localScale = originalBodyScale;
        }
        squashCoroutine = null;
    }

    private void InitializeSparkPool() {
        Color[] pastelColors = new Color[] {
            new Color(1f, 0.65f, 0.65f), // Pastel Red/Pink
            new Color(0.65f, 0.85f, 1f), // Pastel Blue
            new Color(0.65f, 1f, 0.65f), // Pastel Green
            new Color(1f, 0.95f, 0.6f),  // Pastel Yellow
            new Color(0.9f, 0.65f, 1f),  // Pastel Purple
            new Color(1f, 0.75f, 0.5f)   // Pastel Orange
        };

        Shader sparkShader = FindSparkShader();

        // Pre-create materials at startup to avoid runtime allocation
        poolMaterials = new Material[pastelColors.Length];
        for (int i = 0; i < pastelColors.Length; i++) {
            poolMaterials[i] = new Material(sparkShader);
            poolMaterials[i].color = pastelColors[i];
        }

        // Create a root transform to keep hierarchy clean
        GameObject rootObj = new GameObject("SparkPoolRoot");
        rootObj.transform.parent = transform;
        poolRoot = rootObj.transform;

        for (int i = 0; i < maxSparkPoolSize; i++) {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (spark.GetComponent<Collider>() != null) {
                Destroy(spark.GetComponent<Collider>());
            }
            spark.transform.parent = poolRoot;
            spark.SetActive(false);

            Renderer rObj = spark.GetComponent<Renderer>();
            if (rObj != null) {
                rObj.sharedMaterial = poolMaterials[i % poolMaterials.Length];
            }

            sparkPool.Enqueue(spark);
        }
    }

    private GameObject GetPooledSpark() {
        if (sparkPool.Count > 0) {
            GameObject spark = sparkPool.Dequeue();
            if (spark != null) {
                return spark;
            }
        }

        // Expand pool if necessary (dynamic expansion fallback)
        GameObject newSpark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (newSpark.GetComponent<Collider>() != null) {
            Destroy(newSpark.GetComponent<Collider>());
        }
        if (poolRoot != null) {
            newSpark.transform.parent = poolRoot;
        }
        newSpark.SetActive(false);

        Renderer rObj = newSpark.GetComponent<Renderer>();
        if (rObj != null && poolMaterials != null && poolMaterials.Length > 0) {
            rObj.sharedMaterial = poolMaterials[Random.Range(0, poolMaterials.Length)];
        }

        return newSpark;
    }

    private void ReturnSparkToPool(GameObject spark) {
        if (spark == null) return;
        spark.SetActive(false);
        if (poolRoot != null && spark.transform.parent != poolRoot) {
            spark.transform.parent = poolRoot;
        }
        sparkPool.Enqueue(spark);
    }

    private void SpawnCollisionSparks(Vector3 contactPoint, Vector3 contactNormal, float impactSpeed) {
        int count = Mathf.Clamp(Mathf.RoundToInt(impactSpeed * 1.2f), 6, 18);

        for (int i = 0; i < count; i++) {
            GameObject spark = GetPooledSpark();
            if (spark == null) continue;

            spark.transform.position = contactPoint + contactNormal * 0.05f;
            float size = Random.Range(0.06f, 0.16f);
            spark.transform.localScale = new Vector3(size, size, size);
            
            // Set active to start rendering
            spark.SetActive(true);

            Vector3 randomDir = (contactNormal + Random.insideUnitSphere * 0.5f).normalized;
            float speed = Random.Range(1.5f, impactSpeed * 0.5f);
            Vector3 velocity = randomDir * speed;

            StartCoroutine(SparkRoutine(spark, velocity, Random.Range(0.3f, 0.65f)));
        }
    }

    private System.Collections.IEnumerator SparkRoutine(GameObject spark, Vector3 velocity, float lifetime) {
        float elapsed = 0f;
        Vector3 initialScale = spark.transform.localScale;

        while (elapsed < lifetime && spark != null && spark.activeSelf) {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            velocity += Physics.gravity * Time.deltaTime;
            spark.transform.position += velocity * Time.deltaTime;
            spark.transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);

            yield return null;
        }

        ReturnSparkToPool(spark);
    }

    private void EmitDriftSparks() {
        Material targetMat = driftSparkMat0;
        if (driftChargeTime >= driftBoostLevel2Time) {
            targetMat = driftSparkMat2;
        } else if (driftChargeTime >= driftBoostLevel1Time) {
            targetMat = driftSparkMat1;
        }

        EmitWheelSpark(rearLeft, targetMat);
        EmitWheelSpark(rearRight, targetMat);
    }

    private void EmitWheelSpark(Wheel wheel, Material mat) {
        if (!wheel.collider || !wheel.mesh) return;

        GameObject spark = GetPooledSpark();
        if (spark == null) return;

        Vector3 wheelBottom = wheel.mesh.position - wheel.mesh.up * wheel.collider.radius;
        spark.transform.position = wheelBottom;
        
        float size = Random.Range(0.08f, 0.18f);
        spark.transform.localScale = new Vector3(size, size, size);

        Renderer r = spark.GetComponent<Renderer>();
        if (r != null) {
            r.sharedMaterial = mat;
        }

        spark.SetActive(true);

        Vector3 backDir = -transform.forward;
        Vector3 randomSpread = transform.right * Random.Range(-0.4f, 0.4f) + transform.up * Random.Range(0.1f, 0.5f);
        Vector3 velocity = (backDir + randomSpread).normalized * Random.Range(2.5f, 5.5f) + rb.linearVelocity * 0.4f;

        StartCoroutine(SparkRoutine(spark, velocity, Random.Range(0.4f, 0.7f)));
    }

    private void TriggerDriftBoost(int level) {
        float boostForce = level == 2 ? driftBoostLevel2Force : driftBoostLevel1Force;
        float duration = level == 2 ? driftBoostLevel2Duration : driftBoostLevel1Duration;
        float speedMult = level == 2 ? driftBoostLevel2SpeedMult : driftBoostLevel1SpeedMult;

        boostTimeRemaining = duration;
        currentBoostMultiplier = speedMult;

        // Apply velocity impulse forward
        rb.AddForce(transform.forward * boostForce, ForceMode.VelocityChange);

        // Screen shake
        if (CameraFollow.Instance != null) {
            CameraFollow.Instance.TriggerShake(0.2f, level == 2 ? 0.22f : 0.13f);
        }

        // Squash & Stretch speed effect
        if (enableSquashAndStretch && carBodyVisual != null) {
            if (squashCoroutine != null) {
                StopCoroutine(squashCoroutine);
                carBodyVisual.localScale = originalBodyScale;
            }
            squashCoroutine = StartCoroutine(BoostStretchRoutine(duration));
        }
    }

    private System.Collections.IEnumerator BoostStretchRoutine(float duration) {
        if (carBodyVisual == null) yield break;

        float elapsed = 0f;
        Vector3 stretchScale = originalBodyScale;
        stretchScale.z *= 1.25f; // Stretch forward
        stretchScale.x *= 0.85f; // Narrow sides
        stretchScale.y *= 0.85f; // Flat top

        float stretchTime = duration * 0.2f;
        while (elapsed < stretchTime) {
            if (carBodyVisual == null) yield break;
            elapsed += Time.deltaTime;
            carBodyVisual.localScale = Vector3.Lerp(originalBodyScale, stretchScale, elapsed / stretchTime);
            yield return null;
        }

        elapsed = 0f;
        float returnTime = duration * 0.8f;
        while (elapsed < returnTime) {
            if (carBodyVisual == null) yield break;
            elapsed += Time.deltaTime;
            carBodyVisual.localScale = Vector3.Lerp(stretchScale, originalBodyScale, elapsed / returnTime);
            yield return null;
        }

        if (carBodyVisual != null) {
            carBodyVisual.localScale = originalBodyScale;
        }
        squashCoroutine = null;
    }

    private Shader FindSparkShader() {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Mobile/Diffuse");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }

    private void OnDestroy() {
        if (redMaterialInstance != null) {
            Destroy(redMaterialInstance);
        }
        if (driftSparkMat0 != null) {
            Destroy(driftSparkMat0);
        }
        if (driftSparkMat1 != null) {
            Destroy(driftSparkMat1);
        }
        if (driftSparkMat2 != null) {
            Destroy(driftSparkMat2);
        }
        if (poolMaterials != null) {
            foreach (var mat in poolMaterials) {
                if (mat != null) {
                    Destroy(mat);
                }
            }
            poolMaterials = null;
        }
    }
}
