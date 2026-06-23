using System.Collections.Generic;
using UnityEngine;

public class RoadNode {
    public Vector3 position;
    public List<RoadEdge> connectedEdges = new List<RoadEdge>();
    public bool isDestination;

    public RoadNode(Vector3 pos) {
        position = pos;
    }
}