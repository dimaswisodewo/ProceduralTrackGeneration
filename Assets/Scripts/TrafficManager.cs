using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TrafficManager : MonoBehaviour {
    public static TrafficManager Instance { get; private set; }

    [Header("Traffic Population")]
    [Tooltip("Maximum number of active NPC cars allowed at once.")]
    public int maxNPCCount = 10;

    [Header("Spawning & Culling Distances")]
    [Tooltip("Minimum distance from the player to spawn a new NPC.")]
    public float minSpawnDistance = 22f;
    [Tooltip("Maximum distance from the player to spawn a new NPC.")]
    public float maxSpawnDistance = 45f;
    [Tooltip("Distance from the player beyond which NPCs are automatically despawned.")]
    public float despawnDistance = 50f;
    [Tooltip("Interval in seconds between spawn/despawn checks.")]
    public float spawnCheckInterval = 1.0f;

    [Header("Lanes & Offsets")]
    [Tooltip("Offset distance from the center of the road for the keep-right system.")]
    public float laneWidthOffset = 0.65f;
    [Tooltip("Height at which NPCs drive above the road center.")]
    public float npcHeight = 0.75f;

    [Header("NPC Movement Parameters")]
    [Tooltip("Target cruise speed of the NPC cars.")]
    public float targetSpeed = 5.5f;

    private GameObject npcTemplate;
    private List<NPCCarController> activeNPCs = new List<NPCCarController>();
    private Transform playerTransform;
    private bool initialized = false;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject);
        }
    }

    private void Start() {
        StartCoroutine(InitializationRoutine());
    }

    private IEnumerator InitializationRoutine() {
        // Wait until MapGenerator is fully initialized and has road cells
        while (MapGenerator.Instance == null || MapGenerator.Instance.RoadCells.Count == 0) {
            yield return new WaitForSeconds(0.2f);
        }

        // Wait until player car is available in the scene
        while (playerTransform == null) {
            CarController playerCar = FindObjectOfType<CarController>();
            if (playerCar != null) {
                playerTransform = playerCar.transform;
            }
            yield return new WaitForSeconds(0.2f);
        }

        CreateNPCTemplate();
        initialized = true;

        // Populate initially
        for (int i = 0; i < maxNPCCount / 2; i++) {
            SpawnNPC();
        }

        StartCoroutine(TrafficRoutine());
    }

    private void CreateNPCTemplate() {
        CarController playerCar = FindObjectOfType<CarController>();
        if (playerCar == null) {
            Debug.LogError("[TrafficManager] Failed to find Player Car to clone template from!");
            return;
        }

        // Temporarily deactivate the source player car so the clone is created inactive.
        // This prevents the cloned components (like CarInputManager or PackageDeliverySystem) 
        // from executing Awake() and overwriting or destroying active singleton instances.
        bool wasPlayerActive = playerCar.gameObject.activeSelf;
        playerCar.gameObject.SetActive(false);

        // Clone player car to create an NPC base template
        GameObject templateObj = Instantiate(playerCar.gameObject);
        templateObj.name = "NPC_Car_Template";
        templateObj.transform.parent = transform;

        // Restore the player car's active state
        playerCar.gameObject.SetActive(wasPlayerActive);

        // Set layer to "NPC" if defined, otherwise fallback to player's layer
        int npcLayer = LayerMask.NameToLayer("NPC");
        if (npcLayer == -1) {
            Debug.LogWarning("[TrafficManager] Layer 'NPC' is not defined in TagManager. Falling back to player layer.");
            npcLayer = playerCar.gameObject.layer;
        }
        templateObj.layer = npcLayer;
        foreach (Transform child in templateObj.GetComponentsInChildren<Transform>(true)) {
            child.gameObject.layer = npcLayer;
        }

        // Strip player-exclusive controllers and scripts
        DestroyImmediate(templateObj.GetComponent<CarController>());
        DestroyImmediate(templateObj.GetComponent<CarInputManager>());
        
        // Strip other packages/GPS systems
        var pds = templateObj.GetComponent<PackageDeliverySystem>();
        if (pds != null) DestroyImmediate(pds);

        var gps = templateObj.GetComponent<MinimapGPSController>();
        if (gps != null) DestroyImmediate(gps);

        // Strip audio sources to prevent engine noise overlapping
        foreach (var audio in templateObj.GetComponentsInChildren<AudioSource>(true)) {
            DestroyImmediate(audio);
        }

        // Strip WheelColliders since NPCCarController uses kinematic movement
        foreach (var wc in templateObj.GetComponentsInChildren<WheelCollider>(true)) {
            DestroyImmediate(wc);
        }

        // Tag properly as NPC (not Player)
        templateObj.tag = "NPC";
        foreach (Transform child in templateObj.GetComponentsInChildren<Transform>(true)) {
            child.gameObject.tag = "NPC";
        }

        // Add the NPCCarController component
        NPCCarController npcController = templateObj.AddComponent<NPCCarController>();
        npcController.targetSpeed = targetSpeed;

        npcTemplate = templateObj;
    }

    private IEnumerator TrafficRoutine() {
        WaitForSeconds wait = new WaitForSeconds(spawnCheckInterval);
        while (true) {
            if (initialized && playerTransform != null) {
                // 1. Cull far-away NPCs
                Vector3 playerPos = playerTransform.position;
                for (int i = activeNPCs.Count - 1; i >= 0; i--) {
                    NPCCarController npc = activeNPCs[i];
                    if (npc == null) {
                        activeNPCs.RemoveAt(i);
                        continue;
                    }

                    // Only despawn if not currently spun out to avoid visual popping during crashes
                    if (!npc.isSpunOut) {
                        float dist = Vector3.Distance(playerPos, npc.transform.position);
                        if (dist > despawnDistance) {
                            DespawnNPC(npc);
                        }
                    }
                }

                // 2. Replenish traffic population
                if (activeNPCs.Count < maxNPCCount) {
                    SpawnNPC();
                }
            }
            yield return wait;
        }
    }

    private void SpawnNPC() {
        if (npcTemplate == null) return;

        MapGenerator.GridPos spawnCell;
        if (TryFindSpawnCell(out spawnCell)) {
            List<MapGenerator.GridPos> path = GetRandomPath(spawnCell);
            if (path == null || path.Count < 2) return;

            GameObject npcObj = Instantiate(npcTemplate);
            npcObj.name = $"NPC_Car_Instance_{activeNPCs.Count}_{Random.Range(100, 999)}";
            npcObj.transform.parent = transform;

            NPCCarController controller = npcObj.GetComponent<NPCCarController>();
            activeNPCs.Add(controller);

            // Determine relative lane direction (offset value keeps right side)
            float sideOffset = laneWidthOffset;

            controller.InitializePath(path, sideOffset);

            // Give it a fresh custom paint job
            ApplyNPCPaintJob(npcObj);

            npcObj.SetActive(true);
        }
    }

    public void DespawnNPC(NPCCarController npc) {
        if (npc == null) return;
        activeNPCs.Remove(npc);
        Destroy(npc.gameObject);
    }

    public void RequestNewPath(NPCCarController npc) {
        if (npc == null) return;
        
        MapGenerator.GridPos currentCell = GetGridPosFromWorld(npc.transform.position);
        List<MapGenerator.GridPos> path = GetRandomPath(currentCell);

        if (path != null && path.Count > 1) {
            npc.InitializePath(path, npc.laneOffset);
        } else {
            DespawnNPC(npc);
        }
    }

    private bool TryFindSpawnCell(out MapGenerator.GridPos spawnCell) {
        spawnCell = new MapGenerator.GridPos(0, 0);
        if (playerTransform == null) return false;

        var roadCells = MapGenerator.Instance.RoadCells;
        float cellSize = MapGenerator.Instance.cellSize;
        Vector3 playerPos = playerTransform.position;

        float minSqr = minSpawnDistance * minSpawnDistance;
        float maxSqr = maxSpawnDistance * maxSpawnDistance;
        float occupyRadiusSqr = (cellSize * 0.8f) * (cellSize * 0.8f);

        List<MapGenerator.GridPos> candidates = new List<MapGenerator.GridPos>();
        foreach (var cell in roadCells) {
            Vector3 cellPos = new Vector3(cell.x * cellSize, 0f, cell.z * cellSize);
            float sqrDist = (playerPos - cellPos).sqrMagnitude;

            if (sqrDist >= minSqr && sqrDist <= maxSqr) {
                // Ensure we don't spawn right on top of another NPC
                bool isOccupied = false;
                foreach (var npc in activeNPCs) {
                    if (npc != null && (npc.transform.position - cellPos).sqrMagnitude < occupyRadiusSqr) {
                        isOccupied = true;
                        break;
                    }
                }
                if (!isOccupied) {
                    candidates.Add(cell);
                }
            }
        }

        if (candidates.Count > 0) {
            spawnCell = candidates[Random.Range(0, candidates.Count)];
            return true;
        }
        return false;
    }

    public List<MapGenerator.GridPos> GetRandomPath(MapGenerator.GridPos start) {
        var roadCells = MapGenerator.Instance.RoadCells;
        if (roadCells.Count < 2) return null;

        List<MapGenerator.GridPos> roadList = new List<MapGenerator.GridPos>(roadCells);
        MapGenerator.GridPos target = start;

        // Try to pick a destination that is at least 8 units away for a decent drive
        for (int i = 0; i < 30; i++) {
            MapGenerator.GridPos candidate = roadList[Random.Range(0, roadList.Count)];
            float distance = Mathf.Abs(candidate.x - start.x) + Mathf.Abs(candidate.z - start.z);
            if (distance >= 8f) {
                target = candidate;
                break;
            }
        }

        // Fallback if no far cell is found
        if (target.Equals(start)) {
            target = roadList[Random.Range(0, roadList.Count)];
        }

        return FindPathOnRoads(start, target);
    }

    private List<MapGenerator.GridPos> FindPathOnRoads(MapGenerator.GridPos start, MapGenerator.GridPos end) {
        if (MapGenerator.Instance == null) return null;
        return GridPathfinder.FindPath(
            start,
            end,
            MapGenerator.Instance.RoadCells,
            MapGenerator.Instance.SpotCells,
            MapGenerator.Instance.MinX,
            MapGenerator.Instance.MaxX,
            MapGenerator.Instance.MinZ,
            MapGenerator.Instance.MaxZ,
            restrictToRoadsAndSpots: true
        );
    }

    private MapGenerator.GridPos GetGridPosFromWorld(Vector3 worldPos) {
        float cellSize = MapGenerator.Instance.cellSize;
        return new MapGenerator.GridPos(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.z / cellSize)
        );
    }

    private void ApplyNPCPaintJob(GameObject npcObj) {
        Renderer[] renderers = npcObj.GetComponentsInChildren<Renderer>(true);
        NPCCarController controller = npcObj.GetComponent<NPCCarController>();
        
        HashSet<Transform> wheelMeshes = new HashSet<Transform>();
        if (controller != null && controller.wheelRadius > 0) {
            // Find wheels visually to exclude them from body paint
            foreach (Transform child in npcObj.GetComponentsInChildren<Transform>(true)) {
                if (child.name.ToLower().Contains("wheel") && !child.name.ToLower().Contains("collider")) {
                    wheelMeshes.Add(child);
                }
            }
        }

        // Palette of lighter, dull grey colors
        Color[] paintColors = new Color[] {
            new Color(0.42f, 0.42f, 0.42f),   // Muted Medium Grey
            new Color(0.48f, 0.48f, 0.48f),   // Medium Light Grey
            new Color(0.52f, 0.52f, 0.52f),   // Light Slate Grey
            new Color(0.45f, 0.46f, 0.48f),   // Dull Steel Grey
            new Color(0.38f, 0.38f, 0.4f),     // Stone Grey
            new Color(0.56f, 0.56f, 0.58f)    // Bright Dull Grey
        };
        Color paintColor = paintColors[Random.Range(0, paintColors.Length)];

        Material bodyMatInstance = null;
        foreach (var r in renderers) {
            Transform t = r.transform;
            bool isWheel = false;
            while (t != null && t != npcObj.transform) {
                if (wheelMeshes.Contains(t) || t.name.ToLower().Contains("wheel")) {
                    isWheel = true;
                    break;
                }
                t = t.parent;
            }

            if (!isWheel) {
                if (bodyMatInstance == null && r.sharedMaterial != null) {
                    bodyMatInstance = new Material(r.sharedMaterial);
                    bodyMatInstance.color = paintColor;
                    if (bodyMatInstance.HasProperty("_BaseColor")) {
                        bodyMatInstance.SetColor("_BaseColor", paintColor);
                    }
                }
                if (bodyMatInstance != null) {
                    r.sharedMaterial = bodyMatInstance;
                }
            }
        }
    }
}
