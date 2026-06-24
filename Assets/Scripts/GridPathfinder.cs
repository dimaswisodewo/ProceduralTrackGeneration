using UnityEngine;
using System.Collections.Generic;
using GridPos = MapGenerator.GridPos;

public static class GridPathfinder {
    public delegate bool IntersectionCheckDelegate(GridPos neighbor, HashSet<GridPos> currentPath);
    public delegate float HeuristicDelegate(GridPos neighbor);

    /// <summary>
    /// Executes a customizable A* pathfinding search on the grid.
    /// </summary>
    public static List<GridPos> FindPath(
        GridPos start, 
        GridPos end, 
        HashSet<GridPos> roadCells, 
        HashSet<GridPos> spotCells, 
        int minX, int maxX, int minZ, int maxZ,
        bool restrictToRoadsAndSpots,
        IntersectionCheckDelegate wouldCreateIntersection = null,
        HeuristicDelegate customHeuristic = null,
        System.Func<GridPos, bool> stopCondition = null
    ) {
        List<GridPos> openList = new List<GridPos>();
        HashSet<GridPos> closedSet = new HashSet<GridPos>();

        Dictionary<GridPos, GridPos> cameFrom = new Dictionary<GridPos, GridPos>();
        Dictionary<GridPos, float> gScore = new Dictionary<GridPos, float>();
        Dictionary<GridPos, float> fScore = new Dictionary<GridPos, float>();

        gScore[start] = 0f;
        
        float startH = 0f;
        if (customHeuristic != null) {
            startH = customHeuristic(start);
        } else {
            startH = Mathf.Abs(start.x - end.x) + Mathf.Abs(start.z - end.z);
        }
        fScore[start] = startH;
        openList.Add(start);

        while (openList.Count > 0) {
            // Find node with lowest fScore
            GridPos current = openList[0];
            float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;
            for (int i = 1; i < openList.Count; i++) {
                GridPos p = openList[i];
                float f = fScore.ContainsKey(p) ? fScore[p] : float.MaxValue;
                if (f < lowestF) {
                    lowestF = f;
                    current = p;
                }
            }

            // Evaluate stop condition
            bool reachedEnd = false;
            if (stopCondition != null) {
                reachedEnd = stopCondition(current);
            } else {
                reachedEnd = current.Equals(end);
            }

            if (reachedEnd) {
                List<GridPos> path = new List<GridPos>();
                path.Add(current);
                while (cameFrom.ContainsKey(current)) {
                    current = cameFrom[current];
                    path.Add(current);
                }
                path.Reverse();
                return path;
            }

            openList.Remove(current);
            closedSet.Add(current);

            // Pre-calculate current path if needed for intersection prevention check
            HashSet<GridPos> currentPath = null;
            if (wouldCreateIntersection != null) {
                currentPath = new HashSet<GridPos>();
                GridPos temp = current;
                currentPath.Add(temp);
                while (cameFrom.ContainsKey(temp)) {
                    temp = cameFrom[temp];
                    currentPath.Add(temp);
                }
            }

            // Neighbors (orthogonal directions)
            GridPos[] neighbors = new GridPos[] {
                new GridPos(current.x + 1, current.z),
                new GridPos(current.x - 1, current.z),
                new GridPos(current.x, current.z + 1),
                new GridPos(current.x, current.z - 1)
            };

            foreach (GridPos neighbor in neighbors) {
                if (closedSet.Contains(neighbor)) continue;
                if (neighbor.x < minX || neighbor.x > maxX || neighbor.z < minZ || neighbor.z > maxZ) continue;

                if (restrictToRoadsAndSpots) {
                    if (!roadCells.Contains(neighbor) && !spotCells.Contains(neighbor)) {
                        continue;
                    }
                }

                if (wouldCreateIntersection != null && wouldCreateIntersection(neighbor, currentPath)) {
                    continue;
                }

                // Cost calculation
                float moveCost = 1f;
                if (!restrictToRoadsAndSpots) {
                    // Path Coalescing: existing road cells have lower traversal costs
                    moveCost = roadCells.Contains(neighbor) ? 1f : 5f;
                }
                
                float tentativeGScore = gScore[current] + moveCost;

                if (!openList.Contains(neighbor)) {
                    openList.Add(neighbor);
                } else if (tentativeGScore >= (gScore.ContainsKey(neighbor) ? gScore[neighbor] : float.MaxValue)) {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;

                float h = 0f;
                if (customHeuristic != null) {
                    h = customHeuristic(neighbor);
                } else {
                    h = Mathf.Abs(neighbor.x - end.x) + Mathf.Abs(neighbor.z - end.z);
                }
                fScore[neighbor] = tentativeGScore + h;
            }
        }

        return null;
    }
}
