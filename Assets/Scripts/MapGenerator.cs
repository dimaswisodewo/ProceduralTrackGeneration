using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour {
    [Header("Generator Settings")]
    public int numberOfNodes = 200;
    public int numberOfSpots = 10;
    public Vector2 mapSize = new Vector2(200, 200);
    public float cellSize = 3f;

    [Header("Prefabs")]
    public GameObject straightPrefab;
    public GameObject turnPrefab;
    public GameObject tJunctionPrefab;
    public GameObject fillerPrefab;
    public GameObject spotPrefab;

    [System.Serializable]
    public struct GridPos {
        public int x;
        public int z;
        public GridPos(int x, int z) {
            this.x = x;
            this.z = z;
        }
        public override bool Equals(object obj) {
            if (!(obj is GridPos)) return false;
            GridPos p = (GridPos)obj;
            return x == p.x && z == p.z;
        }
        public override int GetHashCode() {
            return x * 31 + z;
        }
        public override string ToString() {
            return "(" + x + ", " + z + ")";
        }
    }

    [Header("Building Settings")]
    public bool generateBuildings = true;
    public float minBuildingHeight = 3f;
    public float maxBuildingHeight = 12f;

    private List<Material> buildingMaterials = new List<Material>();

    private List<RoadNode> nodes = new List<RoadNode>();
    private List<RoadEdge> edges = new List<RoadEdge>();

    private int minX;
    private int maxX;
    private int minZ;
    private int maxZ;

    private HashSet<GridPos> roadCells = new HashSet<GridPos>();
    private HashSet<GridPos> spotCells = new HashSet<GridPos>();
    private List<DeliverySpot> spawnedDeliverySpots = new List<DeliverySpot>();

    public static MapGenerator Instance { get; private set; }

    public HashSet<GridPos> RoadCells => roadCells;
    public HashSet<GridPos> SpotCells => spotCells;
    public int MinX => minX;
    public int MaxX => maxX;
    public int MinZ => minZ;
    public int MaxZ => maxZ;

    private CarController cachedCarController;

    private void Awake() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        Instance = this;
        
        // Find the car and deactivate it during map generation
        cachedCarController = FindObjectOfType<CarController>();
        if (cachedCarController == null) {
            CarController[] cars = FindObjectsOfType<CarController>(true);
            if (cars != null && cars.Length > 0) {
                cachedCarController = cars[0];
            }
        }

        if (cachedCarController != null) {
            cachedCarController.gameObject.SetActive(false);
        }
    }

    void Start() {
        StartCoroutine(GenerateMapCoroutine());
    }

    private System.Collections.IEnumerator GenerateMapCoroutine() {
        // Show generation/black panel
        if (UIManager.Instance != null) {
            UIManager.Instance.SetGenerationPanelActive(true);
        }

        // Wait one frame to let the UIManager display/render the panel
        yield return null;

        // Grid setup based on cell size
        minX = Mathf.RoundToInt(-mapSize.x / (cellSize * 2f));
        maxX = Mathf.RoundToInt(mapSize.x / (cellSize * 2f));
        minZ = Mathf.RoundToInt(-mapSize.y / (cellSize * 2f));
        maxZ = Mathf.RoundToInt(mapSize.y / (cellSize * 2f));

        spawnedDeliverySpots.Clear();

        InitializeBuildingMaterials();

        GenerateNodes();
        ConnectNodes();
        SpawnRoadPrefabs();

        // Place the car on top of the first generated road piece
        if (nodes.Count > 0) {
            Vector3 startPos = nodes[0].position;
            startPos.y = 1.0f; // Sits slightly above the road surface to prevent clipping under gravity

            Quaternion startRot = Quaternion.identity;
            Vector3 roadDir = Vector3.forward;
            if (nodes[0].connectedEdges.Count > 0) {
                // Find which orthogonal neighbor cell from nodes[0] is in roadCells/spotCells to align rotation orthogonally
                GridPos startGridPos = GetGridCoords(nodes[0].position);
                GridPos[] orthogonal = new GridPos[] {
                    new GridPos(startGridPos.x, startGridPos.z + 1), // North
                    new GridPos(startGridPos.x + 1, startGridPos.z), // East
                    new GridPos(startGridPos.x, startGridPos.z - 1), // South
                    new GridPos(startGridPos.x - 1, startGridPos.z)  // West
                };
                
                foreach (var n in orthogonal) {
                    if (roadCells.Contains(n) || spotCells.Contains(n)) {
                        Vector3 neighborPos = new Vector3(n.x * cellSize, 0f, n.z * cellSize);
                        Vector3 dir = (neighborPos - nodes[0].position).normalized;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.001f) {
                            roadDir = dir;
                            break;
                        }
                    }
                }
            }
            startRot = Quaternion.LookRotation(roadDir, Vector3.up);

            if (cachedCarController == null) {
                cachedCarController = FindObjectOfType<CarController>();
                if (cachedCarController == null) {
                    CarController[] cars = FindObjectsOfType<CarController>(true);
                    if (cars != null && cars.Length > 0) {
                        cachedCarController = cars[0];
                    }
                }
            }

            if (cachedCarController != null) {
                cachedCarController.gameObject.SetActive(true);
                cachedCarController.SetInitialPosition(startPos, startRot);
            }
        }

        // Initialize the Package Delivery System
        PackageDeliverySystem deliverySystem = PackageDeliverySystem.Instance;
        if (deliverySystem == null) {
            GameObject carObj = null;
            if (cachedCarController != null) {
                carObj = cachedCarController.gameObject;
            } else {
                carObj = GameObject.Find("Car");
            }
            
            if (carObj != null) {
                carObj.tag = "Player";
                deliverySystem = carObj.GetComponent<PackageDeliverySystem>();
                if (deliverySystem == null) {
                    deliverySystem = carObj.AddComponent<PackageDeliverySystem>();
                }
            }
        }

        if (deliverySystem != null) {
            deliverySystem.InitializeSystem(spawnedDeliverySpots);
        } else {
            Debug.LogError("[MapGenerator] Failed to find Car to attach PackageDeliverySystem!");
        }

        // Initialize the NPC Traffic System
        TrafficManager trafficManager = FindObjectOfType<TrafficManager>();
        if (trafficManager == null) {
            GameObject trafficObj = new GameObject("TrafficManager");
            trafficObj.AddComponent<TrafficManager>();
        }

        // Wait another frame to let cameras/physics settle
        yield return null;

        if (UIManager.Instance != null) {
            UIManager.Instance.SetGenerationPanelActive(false);
        }
    }

    void GenerateNodes() {
        nodes.Clear();
        HashSet<GridPos> occupied = new HashSet<GridPos>();
        
        // Calculate grid area and estimate a safe target number of nodes to avoid crowding
        int gridWidth = maxX - minX;
        int gridHeight = maxZ - minZ;
        int gridArea = gridWidth * gridHeight;
        int maxSafeNodes = Mathf.Max(3, gridArea / 16); // Up to 6% density
        int targetNodes = Mathf.Clamp(numberOfNodes, 3, maxSafeNodes);
        
        // Randomly scatter nodes within the map bounds, snapped to the grid
        for (int i = 0; i < targetNodes; i++) {
            GridPos pos = new GridPos(0, 0);
            bool found = false;
            for (int attempt = 0; attempt < 100; attempt++) {
                int gx = Random.Range(minX + 2, maxX - 2); // inset slightly to avoid edges
                int gz = Random.Range(minZ + 2, maxZ - 2);
                pos = new GridPos(gx, gz);
                if (!occupied.Contains(pos)) {
                    found = true;
                    break;
                }
            }
            if (!found) continue;

            occupied.Add(pos);
            Vector3 worldPos = new Vector3(pos.x * cellSize, 0f, pos.z * cellSize);
            nodes.Add(new RoadNode(worldPos));
        }
    }

    // Union-Find / Disjoint Set structure helper
    private class DisjointSet {
        private int[] parent;
        public DisjointSet(int size) {
            parent = new int[size];
            for (int i = 0; i < size; i++) parent[i] = i;
        }
        public int Find(int i) {
            if (parent[i] == i) return i;
            return parent[i] = Find(parent[i]);
        }
        public bool Union(int i, int j) {
            int rootI = Find(i);
            int rootJ = Find(j);
            if (rootI != rootJ) {
                parent[rootI] = rootJ;
                return true;
            }
            return false;
        }
    }

    void ConnectNodes() {
        edges.Clear();
        if (nodes.Count < 3) return; // Need at least 3 nodes to close loops and avoid dead ends

        // 1. Generate all possible edges between every pair of nodes
        List<RoadEdge> allPossibleEdges = new List<RoadEdge>();
        for (int i = 0; i < nodes.Count; i++) {
            for (int j = i + 1; j < nodes.Count; j++) {
                allPossibleEdges.Add(new RoadEdge(nodes[i], nodes[j]));
            }
        }

        // 2. Sort edges by distance (ascending) to prefer shorter roads
        allPossibleEdges.Sort((a, b) => a.distance.CompareTo(b.distance));

        // 3. Kruskal's algorithm for Minimum Spanning Tree (MST)
        // Standard MST generation: we ensure all nodes are connected into a single component tree.
        DisjointSet ds = new DisjointSet(nodes.Count);
        int edgesAdded = 0;
        List<RoadEdge> mstEdges = new List<RoadEdge>();
        HashSet<RoadEdge> addedSet = new HashSet<RoadEdge>();

        foreach (var edge in allPossibleEdges) {
            int idxA = nodes.IndexOf(edge.nodeA);
            int idxB = nodes.IndexOf(edge.nodeB);

            if (ds.Union(idxA, idxB)) {
                mstEdges.Add(edge);
                addedSet.Add(edge);
                edgesAdded++;
                if (edgesAdded == nodes.Count - 1) break;
            }
        }

        // Add MST edges to the generator's edges list and populate node connection trackers
        foreach (var edge in mstEdges) {
            edges.Add(edge);
            edge.nodeA.connectedEdges.Add(edge);
            edge.nodeB.connectedEdges.Add(edge);
        }

        // 4. Eliminate dead ends by finding all degree-1 nodes and connecting them to their nearest neighbor.
        // We first try to enforce the degree constraint (< 3) on the target node to prevent 4-way intersections.
        bool degreeOneExists = true;
        int safetyLimit = 100;
        while (degreeOneExists && safetyLimit > 0) {
            safetyLimit--;
            degreeOneExists = false;

            List<RoadNode> leafNodes = new List<RoadNode>();
            foreach (var node in nodes) {
                if (node.connectedEdges.Count == 1) {
                    leafNodes.Add(node);
                    degreeOneExists = true;
                }
            }

            if (leafNodes.Count == 0) break;

            foreach (var leaf in leafNodes) {
                // Check if this leaf node's degree was already raised during this cycle
                if (leaf.connectedEdges.Count != 1) continue;

                RoadNode bestTarget = null;
                float minDistance = float.MaxValue;
                RoadEdge bestEdge = null;

                // Find the closest node that isn't already directly connected to the leaf node
                foreach (var edge in allPossibleEdges) {
                    if (edge.nodeA == leaf || edge.nodeB == leaf) {
                        RoadNode other = (edge.nodeA == leaf) ? edge.nodeB : edge.nodeA;

                        // Check direct connection
                        bool alreadyConnected = false;
                        foreach (var e in leaf.connectedEdges) {
                            if (e.nodeA == other || e.nodeB == other) {
                                alreadyConnected = true;
                                break;
                            }
                        }

                        // Target node must have degree < 3 to avoid creating a 4-way intersection
                        if (!alreadyConnected && other.connectedEdges.Count < 3 && edge.distance < minDistance) {
                            minDistance = edge.distance;
                            bestTarget = other;
                            bestEdge = edge;
                        }
                    }
                }

                // Fallback: If no neighbor has degree < 3, connect to the nearest unconnected neighbor regardless of degree to guarantee a closed loop
                if (bestEdge == null) {
                    minDistance = float.MaxValue;
                    foreach (var edge in allPossibleEdges) {
                        if (edge.nodeA == leaf || edge.nodeB == leaf) {
                            RoadNode other = (edge.nodeA == leaf) ? edge.nodeB : edge.nodeA;

                            bool alreadyConnected = false;
                            foreach (var e in leaf.connectedEdges) {
                                if (e.nodeA == other || e.nodeB == other) {
                                    alreadyConnected = true;
                                    break;
                                }
                            }

                            if (!alreadyConnected && edge.distance < minDistance) {
                                minDistance = edge.distance;
                                bestTarget = other;
                                bestEdge = edge;
                            }
                        }
                    }
                }

                if (bestEdge != null) {
                    edges.Add(bestEdge);
                    bestEdge.nodeA.connectedEdges.Add(bestEdge);
                    bestEdge.nodeB.connectedEdges.Add(bestEdge);
                    addedSet.Add(bestEdge);
                }
            }
        }

        // 5. Add some extra random loops for realism (e.g., 20% chance for close-enough edges)
        // Enforce the degree constraint (< 3) on both endpoints to preserve the T-Junction limit.
        foreach (var edge in allPossibleEdges) {
            if (addedSet.Contains(edge)) continue;

            // Only consider relatively close nodes
            float maxDistance = (mapSize.x + mapSize.y) * 0.25f;
            if (edge.distance < maxDistance && Random.value < 0.2f) {
                if (edge.nodeA.connectedEdges.Count < 3 && edge.nodeB.connectedEdges.Count < 3) {
                    edges.Add(edge);
                    edge.nodeA.connectedEdges.Add(edge);
                    edge.nodeB.connectedEdges.Add(edge);
                    addedSet.Add(edge);
                }
            }
        }
    }

    GridPos GetGridCoords(Vector3 worldPos) {
        return new GridPos(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.z / cellSize)
        );
    }

    float Heuristic(GridPos a, GridPos b) {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z);
    }

    List<GridPos> FindGridPath(GridPos start, GridPos end) {
        return GridPathfinder.FindPath(
            start,
            end,
            roadCells,
            spotCells,
            minX,
            maxX,
            minZ,
            maxZ,
            restrictToRoadsAndSpots: false,
            wouldCreateIntersection: WouldCreate4WayIntersection
        );
    }

    /// <summary>
    /// Checks if adding a candidate neighbor cell to the road path would create a 4-way intersection.
    /// It queries a tentative road layout by combining already finalized road cells and the pre-computed current path.
    /// </summary>
    private bool WouldCreate4WayIntersection(GridPos neighbor, HashSet<GridPos> currentPath) {
        // Count connections at the candidate cell itself
        int neighborConnections = 0;
        GridPos[] orthogonalToNeighbor = new GridPos[] {
            new GridPos(neighbor.x + 1, neighbor.z),
            new GridPos(neighbor.x - 1, neighbor.z),
            new GridPos(neighbor.x, neighbor.z + 1),
            new GridPos(neighbor.x, neighbor.z - 1)
        };
        foreach (var n in orthogonalToNeighbor) {
            if (roadCells.Contains(n) || currentPath.Contains(n)) {
                neighborConnections++;
            }
        }
        if (neighborConnections >= 4) {
            return true;
        }

        // Count connections at neighbor's orthogonal neighbors to ensure they are not pushed to 4 connections
        foreach (var n in orthogonalToNeighbor) {
            if (roadCells.Contains(n) || currentPath.Contains(n)) {
                int nConnections = 0;
                GridPos[] orthogonalToN = new GridPos[] {
                    new GridPos(n.x + 1, n.z),
                    new GridPos(n.x - 1, n.z),
                    new GridPos(n.x, n.z + 1),
                    new GridPos(n.x, n.z - 1)
                };
                foreach (var on in orthogonalToN) {
                    if (roadCells.Contains(on) || currentPath.Contains(on) || on.Equals(neighbor)) {
                        nConnections++;
                    }
                }
                if (nConnections >= 4) {
                    return true;
                }
            }
        }

        return false;
    }

    void SpawnRoadPrefabs() {
        roadCells.Clear();
        spotCells.Clear();

        // 1. Trace paths for all edges
        foreach (var edge in edges) {
            GridPos start = GetGridCoords(edge.nodeA.position);
            GridPos end = GetGridCoords(edge.nodeB.position);

            List<GridPos> path = FindGridPath(start, end);
            if (path == null) {
                Debug.LogWarning($"[MapGenerator] Failed to find grid path between node at {start} and node at {end}!");
                continue;
            }

            for (int i = 0; i < path.Count; i++) {
                roadCells.Add(path[i]);
            }
        }

        // Generate and connect the customizable spots
        GenerateAndConnectSpots();

        // Spawn a single large ground plane covering the whole area
        SpawnGroundPlane();

        // 2. Instantiate prefabs
        for (int gx = minX; gx <= maxX; gx++) {
            for (int gz = minZ; gz <= maxZ; gz++) {
                GridPos current = new GridPos(gx, gz);
                Vector3 position = new Vector3(gx * cellSize, 0f, gz * cellSize);

                if (spotCells.Contains(current)) {
                    // Check orthogonal neighbors for road cells to orient the spot
                    HashSet<GridPos> roadNeighbors = new HashSet<GridPos>();
                    GridPos[] orthogonal = new GridPos[] {
                        new GridPos(current.x + 1, current.z),
                        new GridPos(current.x - 1, current.z),
                        new GridPos(current.x, current.z + 1),
                        new GridPos(current.x, current.z - 1)
                    };
                    foreach (var n in orthogonal) {
                        if (roadCells.Contains(n)) {
                            roadNeighbors.Add(n);
                        }
                    }
                    SpawnSpotPiece(current, position, roadNeighbors);
                } else if (roadCells.Contains(current)) {
                    // Check all 4 orthogonal neighbors
                    HashSet<GridPos> validNeighbors = new HashSet<GridPos>();
                    GridPos[] orthogonal = new GridPos[] {
                        new GridPos(current.x + 1, current.z),
                        new GridPos(current.x - 1, current.z),
                        new GridPos(current.x, current.z + 1),
                        new GridPos(current.x, current.z - 1)
                    };
                    foreach (var n in orthogonal) {
                        if (roadCells.Contains(n)) {
                            validNeighbors.Add(n);
                        }
                    }
                    SpawnRoadPiece(current, position, validNeighbors);
                } else {
                    // Fill empty space with Buildings
                    if (generateBuildings) {
                        bool adjacentToRoad = IsAdjacentToRoad(gx, gz);
                        SpawnBuilding(position, adjacentToRoad);
                    }
                }
            }
        }
    }

    bool IsAdjacentToRoad(int gx, int gz) {
        GridPos[] neighbors = new GridPos[] {
            new GridPos(gx + 1, gz),
            new GridPos(gx - 1, gz),
            new GridPos(gx, gz + 1),
            new GridPos(gx, gz - 1)
        };
        foreach (var n in neighbors) {
            if (roadCells.Contains(n) || spotCells.Contains(n)) {
                return true;
            }
        }
        return false;
    }

    void SpawnRoadPiece(GridPos current, Vector3 position, HashSet<GridPos> neighbors) {
        bool hasNorth = neighbors.Contains(new GridPos(current.x, current.z + 1));
        bool hasSouth = neighbors.Contains(new GridPos(current.x, current.z - 1));
        bool hasEast = neighbors.Contains(new GridPos(current.x + 1, current.z));
        bool hasWest = neighbors.Contains(new GridPos(current.x - 1, current.z));

        int connectionCount = (hasNorth ? 1 : 0) + (hasSouth ? 1 : 0) + (hasEast ? 1 : 0) + (hasWest ? 1 : 0);

        GameObject prefabToSpawn = null;
        float yRotation = 0f;
        Vector3 localScale = Vector3.one;

        if (connectionCount == 0 || connectionCount == 1) {
            // Dead end or isolated cell -> Spawn Straight road
            prefabToSpawn = straightPrefab;
            if (hasEast || hasWest) {
                yRotation = 90f;
            } else {
                yRotation = 0f;
            }
        } 
        else if (connectionCount == 2) {
            // Opposite directions -> Spawn Straight
            if (hasNorth && hasSouth) {
                prefabToSpawn = straightPrefab;
                yRotation = 0f;
            } else if (hasEast && hasWest) {
                prefabToSpawn = straightPrefab;
                yRotation = 90f;
            } 
            // Corners (Turns)
            else {
                prefabToSpawn = turnPrefab;
                
                // Rotations are based on the left turn prefab connecting South and West.
                if (hasSouth && hasWest) {
                    // Left Turn: connects South and West
                    yRotation = 0f;
                    localScale = Vector3.one;
                } else if (hasWest && hasNorth) {
                    // Left Turn: connects West and North
                    yRotation = 90f;
                    localScale = Vector3.one;
                } else if (hasNorth && hasEast) {
                    // Left Turn: connects North and East
                    yRotation = 180f;
                    localScale = Vector3.one;
                } else if (hasEast && hasSouth) {
                    // Right Turn: connects East and South. 
                    // Per project rules, we mirror the Left Turn prefab (localScale.x = -1) and apply 0 rotation.
                    yRotation = 0f;
                    localScale = new Vector3(-1f, 1f, 1f); // Mirror along X
                }
            }
        } 
        else if (connectionCount == 3) {
            // T-Junctions: Connects 3 edges using the tJunctionPrefab
            prefabToSpawn = tJunctionPrefab;
            if (!hasNorth) {
                // Connects South, West, East
                yRotation = 0f;
            } else if (!hasEast) {
                // Connects South, West, North
                yRotation = 90f;
            } else if (!hasSouth) {
                // Connects West, North, East
                yRotation = 180f;
            } else if (!hasWest) {
                // Connects North, East, South
                yRotation = 270f;
            }
        } 
        else if (connectionCount == 4) {
            // 4-Way intersection -> Fallback to Filler (which is a flat 2x2 plane).
            // NOTE: With 4-way prevention active in A* and ConnectNodes, this branch should not be reached.
            prefabToSpawn = fillerPrefab;
            yRotation = 0f;
        }

        if (prefabToSpawn != null) {
            GameObject spawned = Instantiate(prefabToSpawn, position, Quaternion.Euler(0f, yRotation, 0f), transform);
            float scaleFactor = cellSize / 2f;
            spawned.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z * scaleFactor);
            
            if (CullingManager.Instance != null) {
                CullingManager.Instance.RegisterCullable(spawned, position);
            }
            
            // If the piece has a RoadPiece component, we can initialize its fields if needed
            RoadPiece roadPiece = spawned.GetComponent<RoadPiece>();
            if (roadPiece != null) {
                // Entry/Exit transforms are already configured on the prefabs,
                // but we can query them or interact with them here if needed.
            }
        }
    }

    public void SnapPrefab(RoadPiece newPiece, Transform previousExitPoint) {
        // 1. Match the rotation so the new piece faces the right way
        // We rotate the new piece so its entry point aligns inversely with the previous exit
        Quaternion rotationOffset = Quaternion.Inverse(newPiece.entryPoint.localRotation);
        newPiece.transform.rotation = previousExitPoint.rotation * rotationOffset;

        // 2. Match the position
        // Calculate the difference between the prefab's root and its entry point,
        // then apply that offset to the target exit point.
        Vector3 positionOffset = newPiece.transform.position - newPiece.entryPoint.position;
        newPiece.transform.position = previousExitPoint.position + positionOffset;
    }

    void GenerateAndConnectSpots() {
        int spotsPlaced = 0;
        int maxAttempts = 100;
        
        for (int i = 0; i < numberOfSpots; i++) {
            bool success = false;
            for (int attempt = 0; attempt < maxAttempts; attempt++) {
                int gx = Random.Range(minX + 2, maxX - 2);
                int gz = Random.Range(minZ + 2, maxZ - 2);
                GridPos candidate = new GridPos(gx, gz);
                
                if (roadCells.Contains(candidate) || spotCells.Contains(candidate)) {
                    continue;
                }
                
                List<GridPos> path = FindPathToRoad(candidate);
                if (path != null && path.Count > 0) {
                    spotCells.Add(candidate);
                    
                    foreach (var cell in path) {
                        if (!cell.Equals(candidate)) {
                            roadCells.Add(cell);
                        }
                    }
                    
                    spotsPlaced++;
                    success = true;
                    break;
                }
            }
            if (!success) {
                Debug.LogWarning($"[MapGenerator] Failed to place spot {i + 1} after {maxAttempts} attempts.");
            }
        }
        Debug.Log($"[MapGenerator] Successfully placed {spotsPlaced} out of {numberOfSpots} spots.");
    }

    float HeuristicToRoad(GridPos pos) {
        float minD = float.MaxValue;
        foreach (var rc in roadCells) {
            float d = Mathf.Abs(pos.x - rc.x) + Mathf.Abs(pos.z - rc.z);
            if (d < minD) minD = d;
        }
        return minD == float.MaxValue ? 0f : minD;
    }

    List<GridPos> FindPathToRoad(GridPos start) {
        if (roadCells.Count == 0) return null;

        return GridPathfinder.FindPath(
            start,
            default(GridPos),
            roadCells,
            spotCells,
            minX,
            maxX,
            minZ,
            maxZ,
            restrictToRoadsAndSpots: false,
            wouldCreateIntersection: WouldCreate4WayIntersection,
            customHeuristic: HeuristicToRoad,
            stopCondition: (pos) => roadCells.Contains(pos)
        );
    }

    void SpawnSpotPiece(GridPos current, Vector3 position, HashSet<GridPos> neighbors) {
        GameObject spawnedSpotObj = null;

        if (spotPrefab == null) {
            // Spawn a small default cube as a placeholder spot destination
            spawnedSpotObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawnedSpotObj.transform.position = position + new Vector3(0f, 0.5f, 0f); // Center and sit on the ground plane
            spawnedSpotObj.transform.localScale = new Vector3(1f, 1f, 1f);
            spawnedSpotObj.transform.parent = transform;
            spawnedSpotObj.name = $"SpotPlaceholder_{current.x}_{current.z}";
        } else {
            bool hasNorth = neighbors.Contains(new GridPos(current.x, current.z + 1));
            bool hasSouth = neighbors.Contains(new GridPos(current.x, current.z - 1));
            bool hasEast = neighbors.Contains(new GridPos(current.x + 1, current.z));
            bool hasWest = neighbors.Contains(new GridPos(current.x - 1, current.z));

            float yRotation = 0f;
            if (hasSouth) {
                yRotation = 0f;
            } else if (hasWest) {
                yRotation = 90f;
            } else if (hasNorth) {
                yRotation = 180f;
            } else if (hasEast) {
                yRotation = 270f;
            }

            spawnedSpotObj = Instantiate(spotPrefab, position, Quaternion.Euler(0f, yRotation, 0f), transform);
            float scaleFactor = cellSize / 2f;
            spawnedSpotObj.transform.localScale = new Vector3(scaleFactor, 1f, scaleFactor);
        }

        if (spawnedSpotObj != null) {
            spawnedSpotObj.tag = "Spot";
            DeliverySpot spotComponent = spawnedSpotObj.AddComponent<DeliverySpot>();
            spawnedDeliverySpots.Add(spotComponent);
        }
    }

    void SpawnGroundPlane() {
        if (fillerPrefab == null) return;

        // Total grid size: (maxX - minX + 1) * cellSize by (maxZ - minZ + 1) * cellSize
        float totalWidth = (maxX - minX + 1) * cellSize;
        float totalLength = (maxZ - minZ + 1) * cellSize;

        // Since the fillerPrefab at scale 1 has size 2 units, the scale required is:
        float scaleX = totalWidth / 2f;
        float scaleZ = totalLength / 2f;

        // Calculate dynamic center
        float centerX = (minX + maxX) * cellSize / 2f;
        float centerZ = (minZ + maxZ) * cellSize / 2f;
        Vector3 position = new Vector3(centerX, -0.01f, centerZ);

        GameObject ground = Instantiate(fillerPrefab, position, Quaternion.identity, transform);
        ground.name = "GroundPlane";
        ground.transform.localScale = new Vector3(scaleX, 1f, scaleZ);
    }

    void InitializeBuildingMaterials() {
        if (buildingMaterials != null) {
            foreach (var mat in buildingMaterials) {
                if (mat != null) {
                    Destroy(mat);
                }
            }
            buildingMaterials.Clear();
        } else {
            buildingMaterials = new List<Material>();
        }

        if (!generateBuildings) return;

        Shader buildingShader = Shader.Find("Universal Render Pipeline/Lit");
        if (fillerPrefab != null) {
            Renderer r = fillerPrefab.GetComponent<Renderer>();
            if (r == null) r = fillerPrefab.GetComponentInChildren<Renderer>();
            if (r != null && r.sharedMaterial != null) {
                buildingShader = r.sharedMaterial.shader;
            }
        } else if (straightPrefab != null) {
            Renderer r = straightPrefab.GetComponent<Renderer>();
            if (r == null) r = straightPrefab.GetComponentInChildren<Renderer>();
            if (r != null && r.sharedMaterial != null) {
                buildingShader = r.sharedMaterial.shader;
            }
        }

        Color[] buildingColors = new Color[] {
            new Color(0.95f, 0.95f, 0.93f), // Warm White / Alabaster
            new Color(0.85f, 0.87f, 0.9f),  // Cool Light Grey
            new Color(0.98f, 0.98f, 0.98f), // Clean White
            new Color(0.96f, 0.95f, 0.92f), // Soft Cream
            new Color(0.8f, 0.82f, 0.85f),  // Light Slate/Silver Grey
            new Color(0.75f, 0.76f, 0.78f), // Medium Light Grey
            new Color(0.88f, 0.87f, 0.85f), // Warm Pale Grey
            new Color(0.91f, 0.91f, 0.93f)  // Pastel Pearl Grey
        };

        for (int i = 0; i < buildingColors.Length; i++) {
            Material mat = new Material(buildingShader);
            mat.color = buildingColors[i];
            
            // Enable GPU Instancing to dramatically reduce draw calls
            mat.enableInstancing = true;
            
            // Adjust smoothness or other shader properties if standard/lit
            if (buildingShader.name.Contains("Lit")) {
                mat.SetFloat("_Smoothness", 0.2f);
            } else {
                mat.SetFloat("_Glossiness", 0.2f);
            }
            buildingMaterials.Add(mat);
        }
    }

    void SpawnBuilding(Vector3 position, bool addCollider) {
        // Create parent building object
        GameObject buildingParent = new GameObject("Building_" + Mathf.RoundToInt(position.x) + "_" + Mathf.RoundToInt(position.z));
        buildingParent.tag = "Building";
        buildingParent.transform.position = position;
        buildingParent.transform.parent = transform;

        if (CullingManager.Instance != null) {
            CullingManager.Instance.RegisterCullable(buildingParent, position);
        }

        // Choose a random shared material for this building
        Material buildingMat = (buildingMaterials != null && buildingMaterials.Count > 0)
            ? buildingMaterials[Random.Range(0, buildingMaterials.Count)]
            : null;

        // Determine base dimensions (slightly less than 2x2 to avoid road overlap)
        float baseWidth = Random.Range(0.7f * cellSize, 0.9f * cellSize);
        float baseDepth = Random.Range(0.7f * cellSize, 0.9f * cellSize);
        float mainHeight = Random.Range(minBuildingHeight, maxBuildingHeight);

        // Add collider to building only if it is adjacent to the road for performance reasons
        if (addCollider) {
            BoxCollider parentCollider = buildingParent.AddComponent<BoxCollider>();
            parentCollider.center = new Vector3(0f, mainHeight / 2f, 0f);
            parentCollider.size = new Vector3(baseWidth, mainHeight, baseDepth);
        }

        // 1. Create Main Tower
        GameObject mainTower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainTower.name = "MainTower";
        if (mainTower.GetComponent<BoxCollider>() != null) {
            DestroyImmediate(mainTower.GetComponent<BoxCollider>());
        }
        mainTower.transform.parent = buildingParent.transform;
        mainTower.transform.localPosition = new Vector3(0f, mainHeight / 2f, 0f);
        mainTower.transform.localScale = new Vector3(baseWidth, mainHeight, baseDepth);
        if (buildingMat != null) {
            mainTower.GetComponent<Renderer>().sharedMaterial = buildingMat;
        }

        // 2. Add extra architectural elements (e.g. tiers or side wings)
        float styleChance = Random.value;
        if (styleChance < 0.4f) {
            // Style A: Tiered / Wedding Cake style (smaller block on top)
            float tierHeight = Random.Range(1.5f, 4f);
            float tierWidth = baseWidth * Random.Range(0.6f, 0.8f);
            float tierDepth = baseDepth * Random.Range(0.6f, 0.8f);
            
            GameObject topTier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topTier.name = "TopTier";
            if (topTier.GetComponent<BoxCollider>() != null) {
                DestroyImmediate(topTier.GetComponent<BoxCollider>());
            }
            topTier.transform.parent = buildingParent.transform;
            topTier.transform.localPosition = new Vector3(0f, mainHeight + (tierHeight / 2f), 0f);
            topTier.transform.localScale = new Vector3(tierWidth, tierHeight, tierDepth);
            if (buildingMat != null) {
                topTier.GetComponent<Renderer>().sharedMaterial = buildingMat;
            }

            // Maybe a small spire on top of that (30% chance)
            if (Random.value < 0.3f) {
                GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spire.name = "Spire";
                if (spire.GetComponent<BoxCollider>() != null) {
                    DestroyImmediate(spire.GetComponent<BoxCollider>());
                }
                spire.transform.parent = buildingParent.transform;
                spire.transform.localPosition = new Vector3(0f, mainHeight + tierHeight + 0.75f, 0f);
                spire.transform.localScale = new Vector3(0.15f, 1.5f, 0.15f);
                if (buildingMat != null) {
                    spire.GetComponent<Renderer>().sharedMaterial = buildingMat;
                }
            }
        } 
        else if (styleChance < 0.7f) {
            // Style B: Side Wing style (lower offset block)
            float wingHeight = mainHeight * Random.Range(0.4f, 0.7f);
            float wingWidth = baseWidth * Random.Range(0.4f, 0.6f);
            float wingDepth = baseDepth * Random.Range(0.4f, 0.6f);
            
            // Choose an offset direction (e.g. North, South, East, West relative to center)
            Vector3 offset = Vector3.zero;
            if (Random.value < 0.5f) {
                offset.x = (baseWidth - wingWidth) / 2f * (Random.value < 0.5f ? 1f : -1f);
            } else {
                offset.z = (baseDepth - wingDepth) / 2f * (Random.value < 0.5f ? 1f : -1f);
            }

            GameObject sideWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideWing.name = "SideWing";
            if (sideWing.GetComponent<BoxCollider>() != null) {
                DestroyImmediate(sideWing.GetComponent<BoxCollider>());
            }
            sideWing.transform.parent = buildingParent.transform;
            sideWing.transform.localPosition = new Vector3(offset.x, wingHeight / 2f, offset.z);
            sideWing.transform.localScale = new Vector3(wingWidth, wingHeight, wingDepth);
            if (buildingMat != null) {
                sideWing.GetComponent<Renderer>().sharedMaterial = buildingMat;
            }
        }
        // Style C: Simple single tower (no extra blocks)
    }

    private void OnDestroy() {
        if (buildingMaterials != null) {
            foreach (var mat in buildingMaterials) {
                if (mat != null) {
                    Destroy(mat);
                }
            }
            buildingMaterials.Clear();
        }
    }
}