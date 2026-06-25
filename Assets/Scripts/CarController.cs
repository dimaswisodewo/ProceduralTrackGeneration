using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

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
    public float maxDriftAngle = 50f;
    public float driftCounterSteerHelper = 0.6f;
    public float driftMaxAngleSteerHelper = 0.45f;
    public float driftStabilizationFactor = 0.3f;
    public float driftCounterSteerTorqueFactor = 1.6f;
    public float maxDriftYawVelocity = 3.5f;
    public float driftFrictionRecoveryRate = 4.0f;

    [Header("Drift Visuals")]
    public float sparkEmitInterval = 0.06f;

    private bool isDrifting = false;
    private float sparkEmitTimer = 0f;
    private float currentRearSidewaysStiffness;

    private ParticleSystem sparkParticleSystem;
    private ParticleSystem.EmitParams sparkEmitParams;
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
    public Vector3 PreviousLinearVelocity { get; private set; }
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

    [Header("Damage Visuals")]
    public ParticleSystem firePrefab;
    public ParticleSystem smokePrefab;
    public Vector3 fireOffset = new Vector3(0f, 0f, 0.5f);
    public Vector3 smokeOffset = new Vector3(0f, 0.5f, 0.5f);

    private ParticleSystem activeFireInstance;
    private ParticleSystem activeSmokeInstance;
    private bool isFireActive = false;

    private bool lastHandbrakeActive = false;
    private bool lastDriftActive = false;

    private float lastBounceTime = 0f;
    private float stunTimer = 0f;
    private Vector3 originalBodyScale;

    private void Start() {
        gameObject.tag = "Player";
        rb = GetComponent<Rigidbody>();
        
        // Enable Rigidbody interpolation for smooth camera follow and rendering
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
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
            currentRearSidewaysStiffness = originalRearLeftSideways.stiffness;
        }

        // Drift spark materials are not needed as we use the ParticleSystem with custom startColor.

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

        // Initialize collision and drift sparks particle system
        sparkParticleSystem = VisualEffectUtility.CreateSparkParticles(transform, Vector3.zero);
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
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ResetWheelState(frontLeft);
        ResetWheelState(frontRight);
        ResetWheelState(rearLeft);
        ResetWheelState(rearRight);

        Physics.SyncTransforms();

        if (CameraFollow.Instance != null) {
            CameraFollow.Instance.ResetCamera();
        }
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

        // Drift sparks
        if (isDrifting) {
            // Spawn drift sparks at interval
            sparkEmitTimer += Time.fixedDeltaTime;
            if (sparkEmitTimer >= sparkEmitInterval) {
                sparkEmitTimer = 0f;
                EmitDriftSparks();
            }
        } else {
            sparkEmitTimer = 0f;
        }

        // Apply helper yaw torque during drift
        if (isDrifting && isGrounded) {
            Vector3 localVelForTorque = transform.InverseTransformDirection(rb.linearVelocity);
            float lateralVelocity = localVelForTorque.x;
            float driftAngle = 0f;
            float speed = rb.linearVelocity.magnitude;
            if (speed > 1f) {
                driftAngle = Mathf.Abs(Mathf.Atan2(lateralVelocity, localVelForTorque.z) * Mathf.Rad2Deg);
            }

            bool isCounterSteering = false;
            if (Mathf.Abs(steering) > 0.05f && Mathf.Abs(lateralVelocity) > 0.5f) {
                isCounterSteering = (lateralVelocity > 0f && steering > 0f) || (lateralVelocity < 0f && steering < 0f);
            }

            float appliedYawForce = driftYawForce;
            if (isCounterSteering) {
                appliedYawForce = driftYawForce * driftCounterSteerTorqueFactor;
            } else {
                float angleRatio = Mathf.Clamp01(driftAngle / maxDriftAngle);
                appliedYawForce = driftYawForce * (1f - angleRatio);
            }

            rb.AddTorque(transform.up * steering * appliedYawForce, ForceMode.Acceleration);

            // Apply opposing stabilizing torque if exceeding max drift angle and not counter-steering
            if (driftAngle > maxDriftAngle && !isCounterSteering) {
                float excessAngle = driftAngle - maxDriftAngle;
                float stabilizeSign = lateralVelocity > 0f ? 1f : -1f;
                float stabilizeTorque = excessAngle * driftStabilizationFactor;
                rb.AddTorque(transform.up * stabilizeSign * stabilizeTorque, ForceMode.Acceleration);
            }

            // Clamp max angular velocity y to prevent uncontrollable spinning
            Vector3 angularVel = rb.angularVelocity;
            angularVel.y = Mathf.Clamp(angularVel.y, -maxDriftYawVelocity, maxDriftYawVelocity);
            rb.angularVelocity = angularVel;
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

        PreviousLinearVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
    }

    private void Update() {
        // Handle Reposition Input
        if (CarInputManager.Instance != null && CarInputManager.Instance.RepositionPressed) {
            RepositionToNearestRoad();
        }

        // Update visual mesh positions and rotations to match physical colliders
        UpdateWheelVisual(frontLeft);
        UpdateWheelVisual(frontRight);
        UpdateWheelVisual(rearLeft);
        UpdateWheelVisual(rearRight);

        // Update damage fire/smoke effects
        UpdateDamageEffects();
    }

    private void ApplyMotorTorque(float torqueInput) {
        // Calculate local forward speed
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardSpeed = localVelocity.z;

        float currentSpeed = Mathf.Abs(forwardSpeed);

        // We limit reverse speed to 40% of max forward speed
        float speedLimit = torqueInput < 0f ? (maxSpeed * 0.4f) : maxSpeed;

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

        // Track last handbrake state for front wheels to avoid unnecessary curve setting
        if (handbrakeActive != lastHandbrakeActive) {
            lastHandbrakeActive = handbrakeActive;
            if (handbrakeActive) {
                SetWheelFriction(frontLeft.collider, 1f, 1f);
                SetWheelFriction(frontRight.collider, 1f, 1f);
            } else {
                SetWheelFriction(frontLeft.collider, originalFrontLeftForward.stiffness, originalFrontLeftSideways.stiffness);
                SetWheelFriction(frontRight.collider, originalFrontRightForward.stiffness, originalFrontRightSideways.stiffness);
            }
        }
        lastDriftActive = driftActive;

        // Target values for rear wheels
        float targetRearForward = originalRearLeftForward.stiffness;
        float targetRearSideways = originalRearLeftSideways.stiffness;

        if (handbrakeActive) {
            targetRearForward = handbrakeForwardStiffness;
            targetRearSideways = handbrakeSidewaysStiffness;
        } else if (driftActive) {
            targetRearForward = originalRearLeftForward.stiffness;
            targetRearSideways = driftRearSidewaysStiffness;
        }

        // Determine how fast rear sideways stiffness shifts
        float lerpSpeed = 15f; // Fast transition into drift/handbrake
        if (!handbrakeActive && !driftActive) {
            // Slower, smooth transition when recovering/straightening
            lerpSpeed = driftFrictionRecoveryRate;
        }

        currentRearSidewaysStiffness = Mathf.MoveTowards(currentRearSidewaysStiffness, targetRearSideways, lerpSpeed * Time.fixedDeltaTime);

        // Apply to rear wheels
        SetWheelFriction(rearLeft.collider, targetRearForward, currentRearSidewaysStiffness);
        SetWheelFriction(rearRight.collider, targetRearForward, currentRearSidewaysStiffness);
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
        if (rb == null) {
            rb = GetComponent<Rigidbody>();
        }
        if (rb != null) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Add a slight height offset to prevent clipping into the road surface
        Vector3 respawnPos = lastSafePosition + Vector3.up * 0.5f;
        transform.position = respawnPos;
        transform.rotation = lastSafeRotation;
        
        if (rb != null) {
            rb.position = respawnPos;
            rb.rotation = lastSafeRotation;
        }

        // Reset steer helper tracker to avoid massive velocity changes on teleport
        lastRotationYaw = lastSafeRotation.eulerAngles.y;

        // Reset wheel colliders to prevent physics glitches upon teleport
        ResetWheelState(frontLeft);
        ResetWheelState(frontRight);
        ResetWheelState(rearLeft);
        ResetWheelState(rearRight);

        RestoreAllFriction();
        if (originalRearLeftSideways.stiffness > 0f) {
            currentRearSidewaysStiffness = originalRearLeftSideways.stiffness;
        }
        lastHandbrakeActive = false;
        lastDriftActive = false;
        isDrifting = false;

        Physics.SyncTransforms();

        if (CameraFollow.Instance != null) {
            CameraFollow.Instance.ResetCamera();
        }

        // Notify PackageDeliverySystem to restore/reset the package state
        if (PackageDeliverySystem.Instance != null) {
            PackageDeliverySystem.Instance.OnCarRespawn();
        }

        // Stop and clear fire/smoke effects immediately on respawn
        StopFireEffectsImmediate();
    }

    private void RepositionToNearestRoad() {
        Vector3 targetPos = lastSafePosition;
        Quaternion targetRot = lastSafeRotation;
        bool foundNearest = false;

        if (MapGenerator.Instance != null) {
            HashSet<MapGenerator.GridPos> roadCells = MapGenerator.Instance.RoadCells;
            HashSet<MapGenerator.GridPos> spotCells = MapGenerator.Instance.SpotCells;
            float cellSize = MapGenerator.Instance.cellSize;

            Vector3 carPos = transform.position;
            Vector3 nearestRoadPos = Vector3.zero;
            float minSqrDistance = float.MaxValue;

            // Search in roadCells
            if (roadCells != null) {
                foreach (var cell in roadCells) {
                    Vector3 roadPos = new Vector3(cell.x * cellSize, 0f, cell.z * cellSize);
                    float sqrDist = (carPos - roadPos).sqrMagnitude;
                    if (sqrDist < minSqrDistance) {
                        minSqrDistance = sqrDist;
                        nearestRoadPos = roadPos;
                        foundNearest = true;
                    }
                }
            }

            // Search in spotCells too
            if (spotCells != null) {
                foreach (var cell in spotCells) {
                    Vector3 spotPos = new Vector3(cell.x * cellSize, 0f, cell.z * cellSize);
                    float sqrDist = (carPos - spotPos).sqrMagnitude;
                    if (sqrDist < minSqrDistance) {
                        minSqrDistance = sqrDist;
                        nearestRoadPos = spotPos;
                        foundNearest = true;
                    }
                }
            }

            if (foundNearest) {
                targetPos = nearestRoadPos;
                
                // Align rotation: keep current yaw, but make roll and pitch flat
                Vector3 forward = transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.001f) {
                    targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                } else {
                    targetRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                }

                // Update tracking variables to this new location
                lastSafePosition = targetPos;
                lastSafeRotation = targetRot;
            }
        }

        // Apply reposition (exactly similar to respawn)
        if (rb == null) {
            rb = GetComponent<Rigidbody>();
        }
        if (rb != null) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 respawnPos = targetPos + Vector3.up * 0.5f;
        transform.position = respawnPos;
        transform.rotation = targetRot;
        
        if (rb != null) {
            rb.position = respawnPos;
            rb.rotation = targetRot;
        }

        // Reset steer helper tracker to avoid massive velocity changes on teleport
        lastRotationYaw = targetRot.eulerAngles.y;

        // Reset wheel colliders to prevent physics glitches upon teleport
        ResetWheelState(frontLeft);
        ResetWheelState(frontRight);
        ResetWheelState(rearLeft);
        ResetWheelState(rearRight);

        RestoreAllFriction();
        if (originalRearLeftSideways.stiffness > 0f) {
            currentRearSidewaysStiffness = originalRearLeftSideways.stiffness;
        }
        lastHandbrakeActive = false;
        lastDriftActive = false;
        isDrifting = false;

        Physics.SyncTransforms();

        if (CameraFollow.Instance != null) {
            CameraFollow.Instance.ResetCamera();
        }

        // Notify PackageDeliverySystem to restore/reset the package state
        if (PackageDeliverySystem.Instance != null) {
            PackageDeliverySystem.Instance.OnCarRespawn();
        }

        // Stop and clear fire/smoke effects immediately on respawn
        StopFireEffectsImmediate();
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
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            float lateralVelocity = localVel.x;
            float driftAngle = 0f;
            float speed = rb.linearVelocity.magnitude;
            if (speed > 1f) {
                driftAngle = Mathf.Abs(Mathf.Atan2(lateralVelocity, localVel.z) * Mathf.Rad2Deg);
            }

            float steering = CarInputManager.Instance != null ? CarInputManager.Instance.Steering : 0f;
            bool isCounterSteering = false;
            if (Mathf.Abs(steering) > 0.05f && Mathf.Abs(lateralVelocity) > 0.5f) {
                isCounterSteering = (lateralVelocity > 0f && steering > 0f) || (lateralVelocity < 0f && steering < 0f);
            }

            if (isCounterSteering) {
                activeSteerHelper = driftCounterSteerHelper;
            } else {
                float angleRatio = Mathf.Clamp01(driftAngle / maxDriftAngle);
                activeSteerHelper = Mathf.Lerp(driftSteerHelper, driftMaxAngleSteerHelper, angleRatio);
            }
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

        // Check tags first as a fast path
        bool isNPC = collision.gameObject.CompareTag("NPC") || (collision.transform.parent != null && collision.transform.parent.CompareTag("NPC"));
        bool isBuilding = collision.gameObject.CompareTag("Building") || (collision.transform.parent != null && collision.transform.parent.CompareTag("Building"));
        bool isSpot = collision.gameObject.CompareTag("Spot") || (collision.transform.parent != null && collision.transform.parent.CompareTag("Spot"));

        if (!isNPC && !isBuilding && !isSpot) {
            // Ignore road/ground pieces
            if (collision.gameObject.GetComponentInParent<RoadPiece>() != null) return;
        }

        Vector3 contactNormal = collision.contacts[0].normal;
        // Filter out floor/ceiling hits (we only bounce off walls/buildings)
        if (Mathf.Abs(contactNormal.y) > 0.6f) return;

        // Get NPC controller if we hit an NPC
        NPCCarController npc = null;
        if (isNPC) {
            npc = collision.gameObject.GetComponentInParent<NPCCarController>();
        }
        bool hitNPC = npc != null;

        // Calculate collision impact speed
        float impactSpeed;
        if (hitNPC) {
            Vector3 playerPrevVel = PreviousLinearVelocity;
            Vector3 npcVel = npc.GetVelocity();
            impactSpeed = (playerPrevVel - npcVel).magnitude;
        } else {
            impactSpeed = collision.relativeVelocity.magnitude;
        }

        // Lower threshold for NPC collisions since same-direction driving reduces relative velocity
        float activeThreshold = hitNPC ? 2f : minBounceSpeed;
        if (impactSpeed < activeThreshold) return;

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
            TriggerSquashAndStretch(contactNormal, impactSpeed);
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

        // Notify PackageDeliverySystem to process collision damage centrally
        if (PackageDeliverySystem.Instance != null) {
            PackageDeliverySystem.Instance.ProcessCollisionDamage(impactSpeed, isNPC);
        }
    }

    private void TriggerSquashAndStretch(Vector3 contactNormal, float impactSpeed) {
        if (!enableSquashAndStretch || carBodyVisual == null) return;

        // Kill active scale tweens on the visual
        carBodyVisual.DOKill();
        carBodyVisual.localScale = originalBodyScale;

        float maxSquashFactor = Mathf.Clamp(impactSpeed * 0.015f, 0.05f, 0.22f);
        float duration = squashDuration;

        float dotForward = Mathf.Abs(Vector3.Dot(contactNormal, transform.forward));
        Vector3 squashScale = originalBodyScale;
        Vector3 stretchScale = originalBodyScale;

        if (dotForward > 0.5f) {
            squashScale.z *= (1f - maxSquashFactor);
            squashScale.y *= (1f + maxSquashFactor * 0.5f);
            squashScale.x *= (1f + maxSquashFactor * 0.5f);

            stretchScale.z *= (1f + maxSquashFactor * 0.4f);
            stretchScale.y *= (1f - maxSquashFactor * 0.2f);
            stretchScale.x *= (1f - maxSquashFactor * 0.2f);
        } else {
            squashScale.x *= (1f - maxSquashFactor);
            squashScale.y *= (1f + maxSquashFactor * 0.5f);
            squashScale.z *= (1f + maxSquashFactor * 0.5f);

            stretchScale.x *= (1f + maxSquashFactor * 0.4f);
            stretchScale.y *= (1f - maxSquashFactor * 0.2f);
            stretchScale.z *= (1f - maxSquashFactor * 0.2f);
        }

        // DOTween Sequence
        Sequence seq = DOTween.Sequence();
        seq.Append(carBodyVisual.DOScale(squashScale, duration * 0.25f).SetEase(Ease.OutQuad));
        seq.Append(carBodyVisual.DOScale(stretchScale, duration * 0.35f).SetEase(Ease.InOutQuad));
        seq.Append(carBodyVisual.DOScale(originalBodyScale, duration * 0.4f).SetEase(Ease.InQuad));
        seq.SetTarget(carBodyVisual);
    }



    private void SpawnCollisionSparks(Vector3 contactPoint, Vector3 contactNormal, float impactSpeed) {
        if (sparkParticleSystem == null) return;

        Color[] pastelColors = new Color[] {
            new Color(1f, 0.65f, 0.65f), // Pastel Red/Pink
            new Color(0.65f, 0.85f, 1f), // Pastel Blue
            new Color(0.65f, 1f, 0.65f), // Pastel Green
            new Color(1f, 0.95f, 0.6f),  // Pastel Yellow
            new Color(0.9f, 0.65f, 1f),  // Pastel Purple
            new Color(1f, 0.75f, 0.5f)   // Pastel Orange
        };

        // Increase particle density/count for a richer burst effect
        int count = Mathf.Clamp(Mathf.RoundToInt(impactSpeed * 3.0f), 15, 50);

        for (int i = 0; i < count; i++) {
            Vector3 randomDir = (contactNormal + Random.insideUnitSphere * 0.5f).normalized;
            // Faster particle velocity for a more dramatic explosion
            float speed = Random.Range(3.0f, impactSpeed * 0.8f);
            Vector3 velocity = randomDir * speed;

            sparkEmitParams.position = contactPoint + contactNormal * 0.05f;
            sparkEmitParams.velocity = velocity;
            sparkEmitParams.startColor = pastelColors[Random.Range(0, pastelColors.Length)];
            sparkEmitParams.startSize = Random.Range(0.12f, 0.32f); // Larger particles
            sparkEmitParams.startLifetime = Random.Range(0.3f, 0.65f);

            sparkParticleSystem.Emit(sparkEmitParams, 1);
        }
    }

    private void EmitDriftSparks() {
        Color targetColor = new Color(0.9f, 0.65f, 1f); // Pastel Purple

        // Spawn a small cluster of sparks per tick for denser trail visual
        for (int i = 0; i < 3; i++) {
            EmitWheelSparkColor(rearLeft, targetColor);
            EmitWheelSparkColor(rearRight, targetColor);
        }
    }

    private void EmitWheelSparkColor(Wheel wheel, Color color) {
        if (!wheel.collider || !wheel.mesh || sparkParticleSystem == null) return;

        Vector3 wheelBottom = wheel.mesh.position - wheel.mesh.up * wheel.collider.radius;
        Vector3 backDir = -transform.forward;
        Vector3 randomSpread = transform.right * Random.Range(-0.4f, 0.4f) + transform.up * Random.Range(0.1f, 0.5f);
        Vector3 velocity = (backDir + randomSpread).normalized * Random.Range(2.5f, 5.5f) + rb.linearVelocity * 0.4f;

        sparkEmitParams.position = wheelBottom;
        sparkEmitParams.velocity = velocity;
        sparkEmitParams.startColor = color;
        sparkEmitParams.startSize = Random.Range(0.15f, 0.30f); // Slightly larger
        sparkEmitParams.startLifetime = Random.Range(0.4f, 0.7f);

        sparkParticleSystem.Emit(sparkEmitParams, 1);
    }





    private void OnDestroy() {
        if (redMaterialInstance != null) {
            Destroy(redMaterialInstance);
        }
        if (sparkParticleSystem != null && sparkParticleSystem.gameObject != null) {
            var psr = sparkParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null) {
                Destroy(psr.sharedMaterial);
            }
        }
        // Clean up procedural fire/smoke materials
        if (activeFireInstance != null && activeFireInstance.gameObject != null) {
            var psr = activeFireInstance.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null) {
                Destroy(psr.sharedMaterial);
            }
        }
        if (activeSmokeInstance != null && activeSmokeInstance.gameObject != null) {
            var psr = activeSmokeInstance.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null) {
                Destroy(psr.sharedMaterial);
            }
        }
    }

    private void UpdateDamageEffects() {
        bool shouldBurn = false;
        if (PackageDeliverySystem.Instance != null) {
            shouldBurn = PackageDeliverySystem.Instance.packageHealth <= 0.01f;
        }

        if (shouldBurn) {
            if (!isFireActive) {
                StartFireEffects();
            }
        } else {
            if (isFireActive) {
                StopFireEffects();
            }
        }
    }

    private void StartFireEffects() {
        isFireActive = true;

        // Fire Effect
        if (activeFireInstance == null) {
            activeFireInstance = VisualEffectUtility.CreateFireParticles(transform, fireOffset, firePrefab);
        }

        // Smoke Effect
        if (activeSmokeInstance == null) {
            activeSmokeInstance = VisualEffectUtility.CreateSmokeParticles(transform, smokeOffset, smokePrefab);
        }

        if (activeFireInstance != null && !activeFireInstance.isPlaying) {
            activeFireInstance.Play(true);
        }
        if (activeSmokeInstance != null && !activeSmokeInstance.isPlaying) {
            activeSmokeInstance.Play(true);
        }
    }

    private void StopFireEffects() {
        isFireActive = false;

        if (activeFireInstance != null) {
            activeFireInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (activeSmokeInstance != null) {
            activeSmokeInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void StopFireEffectsImmediate() {
        isFireActive = false;
        if (activeFireInstance != null) {
            activeFireInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (activeSmokeInstance != null) {
            activeSmokeInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
