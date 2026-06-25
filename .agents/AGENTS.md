# Project Rules & Context: Procedural Road Track Generation

This is a Unity project for generating procedural road tracks. Below is the essential context and structural rules for any AI agent working on this codebase.

## Project Structure & Architecture
- **Environment**: Unity URP (Universal Render Pipeline).
- **Core Script**: [MapGenerator.cs](file:///Users/mdimaswisodewo/Documents/Repository/ProceduralTrackGeneration/Assets/Scripts/MapGenerator.cs) manages the generation pipeline.
- **Grid Layout**: 
  - The road generation uses a **2x2 unit grid** corresponding to the dimensions of the road prefabs.
  - Key nodes are randomly scattered and snapped to this 2x2 grid.

## Generator Pipeline
1. **Node Scattering**: Generates key nodes snapped to grid coordinates. The node count is treated as an estimation and clamped to a safe maximum based on grid space (minimum of 3 nodes).
2. **Kruskal's MST & Dead-End Elimination**: Connects the key nodes into a Minimum Spanning Tree (MST). To guarantee a fully connected circuit with **no dead-end roads**, any node of degree 1 (leaf) is iteratively connected to its closest unconnected neighbor. Additional random edges are added to introduce loop variations.
3. **A\* Pathfinding**: Traces road paths on the grid between connected nodes. Utilizes path coalescing (existing road cells have lower traversal costs) to encourage paths to merge nicely.
4. **Adjacency Connection & Spawning**: Grid cells containing road paths query their **4 orthogonal neighbors** to determine connection counts and orientation. This ensures adjacent roads are automatically merged.

## Road Prefabs & Layout Rules
- **Straight** (`straightPrefab`):
  - Local length = 2 units. Connects opposing edges (e.g. North-South or East-West).
  - Used for straight tracks, dead-ends (fallback), and isolated cells.
- **Turn** (`turnPrefab`):
  - Fits in a 2x2 bounding box.
  - By default, represents a Left Turn.
  - **Right turns** are created by **mirroring** the Left Turn prefab (setting `transform.localScale.x = -1`) and applying `0` rotation.
- **T-Junction** (`tJunctionPrefab`):
  - Fits in a 2x2 bounding box. Connects 3 edges.
- **Filler** (`fillerPrefab`):
  - A flat 2x2 plane. Used to fill non-road space and act as a flat intersection for 4-way crossroad junctions.

## Code Style & Conventions
- Retain standard Unity event methods (`Start`, `Update`, etc.).
- Ensure all positions and measurements align strictly with the 2-unit cell size.
- Maintain graph connectivity rules: do not introduce modifications that cause isolated road segments or floating/misaligned assets.
