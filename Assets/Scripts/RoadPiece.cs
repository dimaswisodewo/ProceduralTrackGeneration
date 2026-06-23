using UnityEngine;

public class RoadPiece : MonoBehaviour {
    public Transform entryPoint;
    public Transform[] exitPoints; // Array allows for T-Junctions (multiple exits)
}