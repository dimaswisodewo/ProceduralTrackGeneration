using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using GridPos = MapGenerator.GridPos;

public class NPCCarController : MonoBehaviour {
    [Header("Movement Settings")]
    public float targetSpeed = 6f;
    public float currentSpeed = 0f;
    public float acceleration = 3f;
    public float rotationSpeed = 8f;
    public float stopThreshold = 1.2f;

    [Header("Avoidance Settings")]
    public float detectionDistance = 7f;
    public Vector3 raycastOffset = new Vector3(0f, 0.4f, 0f);
    public Vector3 raycastExtent = new Vector3(0.6f, 0.4f, 1.2f);

    [Header("Wheels Visuals")]
    public float wheelRadius = 0.35f;

    [Header("Collider Settings")]
    [Tooltip("Scale factor for the NPC body collider to make it smaller than the visual appearance.")]
    public float colliderScaleFactor = 0.7f;

    [Header("States")]
    public bool isSpunOut = false;
    public bool isRecovering = false;
    public bool isPaused = false;
    public float laneOffset = 0.7f;

    private Rigidbody rb;
    private List<MapGenerator.GridPos> currentPath = new List<MapGenerator.GridPos>();
    private int currentPathIndex = 0;
    private Vector3 targetWorldPosition;
    private float spinOutTimer = 0f;
    private float pauseTimer = 0f;
    private float stuckTimer = 0f;
    private Vector3 lastPosition;
    private float frontZOffset = 1.3f;
    private static readonly RaycastHit[] avoidanceHits = new RaycastHit[16];



    private void Awake() {
        rb = GetComponent<Rigidbody>();
        if (rb == null) {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // Ensure there is a body collider for physical interaction and avoidance detection
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool hasBodyCollider = false;
        foreach (var col in colliders) {
            if (col is WheelCollider || IsWheelTransform(col.transform)) {
                // Destroy any wheel colliders so only the body has a collider
                Destroy(col);
                continue;
            }

            hasBodyCollider = true;
            if (!col.isTrigger) {
                ShrinkCollider(col);
            }
        }

        // Dynamically calculate the front bumper z-offset for BoxCast avoidance sweep
        float maxZ = 1.3f;
        foreach (var col in colliders) {
            if (col == null || col is WheelCollider || IsWheelTransform(col.transform)) continue;
            if (col is BoxCollider box) {
                float localColFront = box.center.z + box.size.z * 0.5f;
                Vector3 localPos = transform.InverseTransformPoint(col.transform.TransformPoint(new Vector3(0f, 0f, localColFront)));
                if (localPos.z > maxZ) {
                    maxZ = localPos.z;
                }
            }
        }
        frontZOffset = maxZ;

        if (!hasBodyCollider) {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(1.3f, 0.8f, 2.6f) * colliderScaleFactor;
            frontZOffset = box.center.z + box.size.z * 0.5f;
        }

        // Add a trigger collider to detect kinematic-kinematic NPC collisions
        BoxCollider triggerBox = gameObject.AddComponent<BoxCollider>();
        triggerBox.isTrigger = true;
        triggerBox.center = new Vector3(0f, 0.4f, 0f);
        triggerBox.size = new Vector3(1.3f, 0.8f, frontZOffset * 2f) * colliderScaleFactor;
    }

    private bool IsWheelTransform(Transform t) {
        while (t != null && t != transform) {
            if (t.name.ToLower().Contains("wheel")) {
                return true;
            }
            t = t.parent;
        }
        return false;
    }

    private void ShrinkCollider(Collider col) {
        if (col == null) return;

        if (col is BoxCollider box) {
            box.size *= colliderScaleFactor;
        } else if (col is MeshCollider meshCol) {
            if (meshCol.sharedMesh != null) {
                Bounds localBounds = meshCol.sharedMesh.bounds;
                GameObject colGo = meshCol.gameObject;
                meshCol.enabled = false;
                
                BoxCollider newBox = colGo.AddComponent<BoxCollider>();
                newBox.center = localBounds.center;
                newBox.size = localBounds.size * colliderScaleFactor;
            }
        } else if (col is SphereCollider sphere) {
            sphere.radius *= colliderScaleFactor;
        } else if (col is CapsuleCollider capsule) {
            capsule.radius *= colliderScaleFactor;
            capsule.height *= colliderScaleFactor;
        }
    }

    private void Start() {
        lastPosition = transform.position;
    }

    public void InitializePath(List<MapGenerator.GridPos> path, float sideOffset) {
        if (rb == null) {
            rb = GetComponent<Rigidbody>();
            if (rb == null) {
                rb = gameObject.AddComponent<Rigidbody>();
            }
        }

        currentPath = path;
        currentPathIndex = 0;
        laneOffset = sideOffset;
        isSpunOut = false;
        isPaused = false;
        spinOutTimer = 0f;
        pauseTimer = 0f;
        stuckTimer = 0f;
        currentSpeed = 0f;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (currentPath != null && currentPath.Count > 0) {
            CalculateNextWaypoint();
            
            // Snap to the starting waypoint position
            Vector3 startDir = GetPathDirection(0);
            Vector3 startWaypoint = GetWaypointWorldPosition(currentPath[0], startDir);
            transform.position = startWaypoint;
            lastPosition = startWaypoint;

            if (currentPath.Count > 1) {
                Vector3 nextWaypoint = GetWaypointWorldPosition(currentPath[1], GetPathDirection(1));
                Vector3 lookDir = (nextWaypoint - startWaypoint).normalized;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f) {
                    transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                }
            }
        }
    }

    private void Update() {
        if (isRecovering) return;

        if (isPaused) {
            UpdatePause();
            return;
        }

        if (isSpunOut) {
            UpdateSpinOut();
            return;
        }

        if (currentPath == null || currentPath.Count == 0) return;

        UpdateMovement();
    }

    private void UpdatePause() {
        pauseTimer -= Time.deltaTime;
        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime * 2f);
        
        if (rb != null) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (pauseTimer <= 0f) {
            isPaused = false;
            CalculateNextWaypoint();
        }
    }

    private void UpdateSpinOut() {
        spinOutTimer -= Time.deltaTime;
        
        currentSpeed = rb.linearVelocity.magnitude;

        if (spinOutTimer <= 0f) {
            RecoverFromSpinOut();
        }
    }

    private void UpdateMovement() {
        Vector3 toWaypoint = targetWorldPosition - transform.position;
        toWaypoint.y = 0f;
        float distance = toWaypoint.magnitude;

        if (distance < stopThreshold) {
            currentPathIndex++;
            if (currentPathIndex >= currentPath.Count) {
                TrafficManager.Instance.RequestNewPath(this);
                return;
            } else {
                CalculateNextWaypoint();
                toWaypoint = targetWorldPosition - transform.position;
                toWaypoint.y = 0f;
                distance = toWaypoint.magnitude;
            }
        }

        // Avoidance & Speed control
        float speedFactor = CheckForObstacles();
        float targetS = targetSpeed * speedFactor;
        
        // Decelerate/accelerate smoothly
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetS, acceleration * Time.deltaTime);

        if (currentSpeed > 0.01f && distance > 0.01f) {
            Vector3 moveDirection = toWaypoint.normalized;
            
            // Kinematic movement
            Vector3 newPosition = Vector3.MoveTowards(transform.position, targetWorldPosition, currentSpeed * Time.deltaTime);
            rb.MovePosition(newPosition);

            // Kinematic rotation
            if (moveDirection.sqrMagnitude > 0.001f) {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection, Vector3.up);
                Quaternion newRot = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                rb.MoveRotation(newRot);
            }
        }

        // Stuck detection
        if (currentSpeed < 0.2f && speedFactor > 0.8f) {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 5f) {
                TrafficManager.Instance.DespawnNPC(this);
            }
        } else {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;
    }



    private float CheckForObstacles() {
        // Position the boxcast origin slightly in front of the front bumper to prevent self-overlap
        Vector3 localOrigin = raycastOffset;
        localOrigin.z = frontZOffset + 0.1f;

        Vector3 origin = transform.position + transform.rotation * localOrigin;
        Vector3 direction = transform.forward;
        float checkDist = detectionDistance + (currentSpeed * 0.4f);

        // Keep the box very thin in the Z direction so it sweeps starting from the front bumper
        Vector3 extents = raycastExtent;
        extents.z = 0.1f;

        int hitCount = Physics.BoxCastNonAlloc(origin, extents, direction, avoidanceHits, transform.rotation, checkDist);
        
        float minDistance = float.MaxValue;
        bool obstacleFound = false;

        for (int i = 0; i < hitCount; i++) {
            RaycastHit hit = avoidanceHits[i];
            
            // Ignore ourselves (any collider on this GameObject or its children)
            if (hit.collider == null || hit.collider.transform.root == transform.root) {
                continue;
            }

            // Ignore roads, ground, and buildings
            if (hit.collider.GetComponentInParent<RoadPiece>() != null || 
                hit.collider.name.Contains("GroundPlane") || 
                hit.collider.name.Contains("Building")) {
                continue;
            }

            // Found a valid obstacle (another NPC or the Player)
            if (hit.distance < minDistance) {
                minDistance = hit.distance;
                obstacleFound = true;
            }
        }

        if (obstacleFound) {
            float distFactor = minDistance / checkDist;
            // Stronger slowdown as we get closer
            return Mathf.Clamp01(distFactor * 0.8f);
        }
        return 1f;
    }

    private void CalculateNextWaypoint() {
        if (currentPath == null || currentPathIndex >= currentPath.Count) return;
        Vector3 dir = GetPathDirection(currentPathIndex);
        targetWorldPosition = GetWaypointWorldPosition(currentPath[currentPathIndex], dir);
    }

    private Vector3 GetPathDirection(int index) {
        if (currentPath == null || currentPath.Count == 0) return transform.forward;

        Vector3 dir = Vector3.forward;
        float cellSize = MapGenerator.Instance.cellSize;
        if (index < currentPath.Count - 1) {
            Vector3 currentCellPos = new Vector3(currentPath[index].x * cellSize, 0f, currentPath[index].z * cellSize);
            Vector3 nextCellPos = new Vector3(currentPath[index + 1].x * cellSize, 0f, currentPath[index + 1].z * cellSize);
            dir = (nextCellPos - currentCellPos).normalized;
        } else if (index > 0) {
            Vector3 prevCellPos = new Vector3(currentPath[index - 1].x * cellSize, 0f, currentPath[index - 1].z * cellSize);
            Vector3 currentCellPos = new Vector3(currentPath[index].x * cellSize, 0f, currentPath[index].z * cellSize);
            dir = (currentCellPos - prevCellPos).normalized;
        }
        return dir;
    }

    private Vector3 GetWaypointWorldPosition(MapGenerator.GridPos cell, Vector3 dir) {
        float cellSize = MapGenerator.Instance.cellSize;
        float height = TrafficManager.Instance != null ? TrafficManager.Instance.npcHeight : 0.75f;
        Vector3 cellCenter = new Vector3(cell.x * cellSize, height, cell.z * cellSize);
        
        // Right hand perpendicular: (dz, -dx)
        Vector3 rightPerp = new Vector3(dir.z, 0f, -dir.x).normalized;
        return cellCenter + rightPerp * laneOffset;
    }

    private void OnCollisionEnter(Collision collision) {
        if (isSpunOut || isRecovering) return;

        // 1. Hit by Player
        if (collision.gameObject.CompareTag("Player")) {
            Rigidbody playerRb = collision.rigidbody;
            float impactForce = collision.relativeVelocity.magnitude;
            
            // If collision is forceful enough, spin out physically!
            if (impactForce > 4f) {
                Vector3 relativeVelocityOfPlayer = collision.relativeVelocity;
                
                // Spin out NPC (uses VelocityChange so it ignores mass)
                SpinOut(relativeVelocityOfPlayer, collision.contacts[0].point);
                
                // Add recoil force to player (opposite direction of impact)
                if (playerRb != null) {
                    Vector3 recoilDir = -relativeVelocityOfPlayer.normalized;
                    recoilDir.y = 0.15f; // Add a juicy little upward hop to player
                    recoilDir.Normalize();
                    
                    float playerRecoilSpeed = impactForce * 0.6f; // Recoil multiplier
                    playerRb.AddForce(recoilDir * playerRecoilSpeed, ForceMode.VelocityChange);
                    
                    // Add a tiny random spin torque to the player's car
                    playerRb.AddTorque(Vector3.up * Random.Range(-4f, 4f), ForceMode.VelocityChange);
                }
            } else {
                // Minor bump from player -> Shock-pause for 3 seconds
                TriggerShockPause(3.0f);
            }
        }
        // 2. Hit by another NPC
        else {
            NPCCarController otherNPC = collision.gameObject.GetComponentInParent<NPCCarController>();
            if (otherNPC != null) {
                float impactForce = collision.relativeVelocity.magnitude;
                
                if (otherNPC.isSpunOut && impactForce > 3f) {
                    Vector3 impactDir = collision.relativeVelocity;
                    
                    // Spin out this NPC in the direction of the hit (reduced force for NPC-to-NPC impact)
                    SpinOut(impactDir * 0.3f, collision.contacts[0].point);
                } else {
                    // Minor bump from another NPC -> Shock-pause for 2 seconds
                    TriggerShockPause(2.0f);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (isSpunOut || isRecovering) return;

        // Check if we hit another NPC
        NPCCarController otherNPC = other.gameObject.GetComponentInParent<NPCCarController>();
        if (otherNPC != null && otherNPC != this) {
            // Let OnCollisionEnter handle it if either is already spun out / recovering (since they become dynamic physics bodies)
            if (otherNPC.isSpunOut || otherNPC.isRecovering || isSpunOut || isRecovering) return;

            // Minor bump / Fender bender -> Both shock-pause and recoil slightly using DOTween
            // This prevents them from flying in the air like they do when hit by the player
            TriggerShockPause(1.5f);
            otherNPC.TriggerShockPause(1.5f);

            Vector3 pushDirection = (transform.position - otherNPC.transform.position).normalized;
            pushDirection.y = 0f;
            if (pushDirection.sqrMagnitude < 0.001f) {
                pushDirection = -transform.forward;
            }

            // Push this NPC backward slightly
            transform.DOMove(transform.position + pushDirection * 0.3f, 0.25f).SetEase(Ease.OutQuad);
            // Push the other NPC in the opposite direction slightly
            otherNPC.transform.DOMove(otherNPC.transform.position - pushDirection * 0.3f, 0.25f).SetEase(Ease.OutQuad);
        }
    }

    public void TriggerShockPause(float duration) {
        if (isSpunOut || isRecovering) return;
        isPaused = true;
        pauseTimer = duration;
    }

    public void SpinOut(Vector3 impactVelocity, Vector3 contactPoint) {
        isSpunOut = true;
        spinOutTimer = 2.0f;

        rb.isKinematic = false;
        rb.useGravity = true;

        // Apply physical velocity changes (ignores mass, allowing dramatic launches)
        Vector3 launchVel = impactVelocity * 0.9f;
        launchVel.y = Mathf.Max(launchVel.y, 4f); // Ensure a dramatic vertical pop
        rb.AddForce(launchVel, ForceMode.VelocityChange);

        // Apply a high spin torque using VelocityChange
        rb.AddTorque(new Vector3(Random.Range(-10f, 10f), Random.Range(-35f, 35f), Random.Range(-10f, 10f)), ForceMode.VelocityChange);
    }

    private void RecoverFromSpinOut() {
        isSpunOut = false;
        isRecovering = true;
        
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        MapGenerator.GridPos nearestCell = FindNearestRoadCell(transform.position);

        // Request a fresh path starting from this cell
        List<MapGenerator.GridPos> newPath = TrafficManager.Instance.GetRandomPath(nearestCell);
        if (newPath != null && newPath.Count > 1) {
            // Calculate starting waypoint and rotation
            Vector3 startDir = GetPathDirection(0);
            Vector3 targetPosition = GetWaypointWorldPosition(newPath[0], startDir);
            
            Vector3 lookDir = startDir;
            if (newPath.Count > 1) {
                Vector3 nextWaypoint = GetWaypointWorldPosition(newPath[1], GetPathDirection(1));
                lookDir = (nextWaypoint - targetPosition).normalized;
            }
            lookDir.y = 0f;
            Quaternion targetRotation = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir, Vector3.up) : transform.rotation;

            // Animate position and rotation back onto the lane smoothly
            transform.DOMove(targetPosition, 0.8f).SetEase(Ease.OutQuad);
            transform.DORotateQuaternion(targetRotation, 0.8f).SetEase(Ease.OutQuad).OnComplete(() => {
                isRecovering = false;
                InitializePath(newPath, laneOffset);
                isPaused = true;
                pauseTimer = 3.0f; // Pause for 3 seconds after recovering before moving again
            });
        } else {
            // Despawn if a valid path cannot be computed
            isRecovering = false;
            TrafficManager.Instance.DespawnNPC(this);
        }
    }

    private void OnDestroy() {
        // Kill active tweens to prevent memory leaks or console warnings
        transform.DOKill();
    }

    private MapGenerator.GridPos FindNearestRoadCell(Vector3 pos) {
        float cellSize = MapGenerator.Instance.cellSize;
        var roadCells = MapGenerator.Instance.RoadCells;
        var spotCells = MapGenerator.Instance.SpotCells;

        GridPos snapped = new GridPos(
            Mathf.RoundToInt(pos.x / cellSize),
            Mathf.RoundToInt(pos.z / cellSize)
        );

        // 1. Direct O(1) Hit
        if (roadCells.Contains(snapped) || spotCells.Contains(snapped)) {
            return snapped;
        }

        // 2. O(1) Radial Spiral Search (check up to 10 rings / ~30 units)
        for (int r = 1; r <= 10; r++) {
            // Check top and bottom rows
            for (int dx = -r; dx <= r; dx++) {
                GridPos pTop = new GridPos(snapped.x + dx, snapped.z + r);
                if (roadCells.Contains(pTop) || spotCells.Contains(pTop)) return pTop;

                GridPos pBottom = new GridPos(snapped.x + dx, snapped.z - r);
                if (roadCells.Contains(pBottom) || spotCells.Contains(pBottom)) return pBottom;
            }

            // Check left and right columns (excluding corners)
            for (int dz = -r + 1; dz <= r - 1; dz++) {
                GridPos pRight = new GridPos(snapped.x + r, snapped.z + dz);
                if (roadCells.Contains(pRight) || spotCells.Contains(pRight)) return pRight;

                GridPos pLeft = new GridPos(snapped.x - r, snapped.z + dz);
                if (roadCells.Contains(pLeft) || spotCells.Contains(pLeft)) return pLeft;
            }
        }

        // 3. Fallback: Full linear scan
        MapGenerator.GridPos bestCell = new MapGenerator.GridPos(0, 0);
        float minD = float.MaxValue;

        foreach (var cell in roadCells) {
            Vector3 cellPos = new Vector3(cell.x * cellSize, 0f, cell.z * cellSize);
            float d = Vector3.Distance(pos, cellPos);
            if (d < minD) {
                minD = d;
                bestCell = cell;
            }
        }

        return bestCell;
    }
}
