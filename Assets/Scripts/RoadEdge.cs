using UnityEngine;

public class RoadEdge {
    public RoadNode nodeA;
    public RoadNode nodeB;
    public float distance;

    public RoadEdge(RoadNode a, RoadNode b) {
        nodeA = a;
        nodeB = b;
        distance = Vector3.Distance(a.position, b.position);
    }
}