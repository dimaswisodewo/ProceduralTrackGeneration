using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using GridPos = MapGenerator.GridPos;

public class MinimapGPSController : MonoBehaviour {
    [Header("GPS & Minimap Settings")]
    public float minimapOrthoSize = 40f;
    public float gpsUpdateInterval = 0.15f;
    public float pathHeightOffset = 0.22f;
    public float lineWidth = 0.9f;
    public Color gpsPathColor = new Color(0.85f, 0.68f, 1.0f, 0.95f); // Vibrant Neon Pastel Purple (Lilac)
    public Color pickupIconColor = new Color(0.85f, 0.68f, 1.0f, 1f); // Vibrant Neon Pastel Purple (Lilac)
    public Color deliveryIconColor = new Color(1.0f, 0.75f, 0.55f, 1f); // Vibrant Neon Pastel Orange (Peach)

    [Header("Optimization Settings")]
    [Tooltip("Target frame rate for the minimap rendering. Lower values save GPU power.")]
    public float minimapRenderFPS = 30f;

    // Camera & RenderTexture
    private Camera minimapCamera;
    private RenderTexture minimapRT;

    // UI Elements
    private GameObject minimapPanel;
    private GameObject destIconObj;
    private RectTransform destIconRect;
    private RectTransform playerIconRect;
    private Image destIconImage;

    // GPS Navigation Path
    private LineRenderer pathLineRenderer;
    private float nextGpsUpdateTime = 0f;
    private Transform carTransform;

    // Resource tracking to prevent memory leaks
    private List<Texture2D> generatedTextures = new List<Texture2D>();
    private List<Sprite> generatedSprites = new List<Sprite>();
    private Material gpsLineMaterial;

    private Texture2D RegisterTexture(Texture2D tex) {
        if (tex != null) {
            generatedTextures.Add(tex);
        }
        return tex;
    }

    private Sprite CreateRegisteredSprite(Texture2D texture, Rect rect, Vector2 pivot) {
        Sprite sprite = Sprite.Create(texture, rect, pivot);
        if (sprite != null) {
            generatedSprites.Add(sprite);
        }
        return sprite;
    }

    private void Start() {
        carTransform = this.transform;

        // 1. Setup Minimap Camera and RenderTexture
        SetupMinimapCamera();

        // 2. Setup Minimap UI Overlay
        SetupMinimapUI();

        // 3. Setup LineRenderer for the GPS line
        SetupGPSLineRenderer();

        // 4. Start Minimap rendering rate-limiter coroutine
        StartCoroutine(MinimapRenderRoutine());
    }

    private void OnEnable() {
        // If already initialized and re-enabled (e.g. after template deactivation), restart coroutine
        if (minimapCamera != null && carTransform != null) {
            StopAllCoroutines();
            StartCoroutine(MinimapRenderRoutine());
        }
    }

    private void SetupMinimapCamera() {
        // Create RenderTexture
        minimapRT = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        minimapRT.filterMode = FilterMode.Bilinear;
        minimapRT.Create();

        // Create Camera GameObject
        GameObject camObj = new GameObject("MinimapCamera");
        minimapCamera = camObj.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = minimapOrthoSize;
        minimapCamera.targetTexture = minimapRT;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f); // Dark background matching pastel theme

        // Inherit culling mask from main camera
        if (Camera.main != null) {
            minimapCamera.cullingMask = Camera.main.cullingMask;
            // Force Main Camera to cull Layer 31 (Minimap Only) to hide the GPS line in the 3D world view
            Camera.main.cullingMask &= ~(1 << 31);
        }

        // Force Minimap Camera to render Layer 31
        minimapCamera.cullingMask |= (1 << 31);

        // Force Minimap Camera to cull Layer 30 (Main Camera Only / Hide from Minimap)
        minimapCamera.cullingMask &= ~(1 << 30);

        // Disable standard rendering behavior to manually render in a controlled rate-limited coroutine
        minimapCamera.enabled = false;

        // Place camera high up pointing down
        UpdateCameraPosition();
    }

    private void SetupMinimapUI() {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) {
            Debug.LogError("[MinimapGPSController] Canvas not found in scene!");
            return;
        }

        // 1. Container Panel (Glassmorphism look with glowing border)
        minimapPanel = new GameObject("MinimapPanel");
        minimapPanel.layer = 5; // UI Layer
        minimapPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = minimapPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.sizeDelta = new Vector2(185f, 185f);
        panelRect.anchoredPosition = new Vector2(30f, 30f);

        // Procedural circular border background
        Image borderImage = minimapPanel.AddComponent<Image>();
        borderImage.sprite = CreateRegisteredSprite(
            CreateCircleBorderTexture(256, 12f), 
            new Rect(0, 0, 256, 256), 
            new Vector2(0.5f, 0.5f)
        );
        borderImage.color = new Color(0.15f, 0.15f, 0.18f, 0.9f); // Glass dark base with dark border

        // Add a neon glow outline image to frame the minimap beautifully
        GameObject glowFrame = new GameObject("MinimapGlowFrame");
        glowFrame.layer = 5;
        glowFrame.transform.SetParent(minimapPanel.transform, false);
        RectTransform glowRect = glowFrame.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.sizeDelta = Vector2.zero;
        glowRect.anchoredPosition = Vector2.zero;
        Image glowImg = glowFrame.AddComponent<Image>();
        glowImg.sprite = CreateRegisteredSprite(
            CreateCircleBorderOutlineTexture(256, 4f),
            new Rect(0, 0, 256, 256),
            new Vector2(0.5f, 0.5f)
        );
        glowImg.color = gpsPathColor; // Neon cyan outline matches GPS path

        // 2. Circular Mask
        GameObject maskObj = new GameObject("MinimapMask");
        maskObj.layer = 5;
        maskObj.transform.SetParent(minimapPanel.transform, false);

        RectTransform maskRect = maskObj.AddComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.sizeDelta = new Vector2(-16f, -16f); // Inset slightly from the border
        maskRect.anchoredPosition = Vector2.zero;

        Image maskImage = maskObj.AddComponent<Image>();
        maskImage.sprite = CreateRegisteredSprite(
            CreateCircleTexture(256), 
            new Rect(0, 0, 256, 256), 
            new Vector2(0.5f, 0.5f)
        );

        Mask mask = maskObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // 3. RawImage to show Minimap RenderTexture inside the mask
        GameObject rawImageObj = new GameObject("MinimapRawImage");
        rawImageObj.layer = 5;
        rawImageObj.transform.SetParent(maskObj.transform, false);

        RectTransform rawImageRect = rawImageObj.AddComponent<RectTransform>();
        rawImageRect.anchorMin = Vector2.zero;
        rawImageRect.anchorMax = Vector2.one;
        rawImageRect.sizeDelta = Vector2.zero;
        rawImageRect.anchoredPosition = Vector2.zero;

        RawImage rawImage = rawImageObj.AddComponent<RawImage>();
        rawImage.texture = minimapRT;

        // Shared 64x64 circle sprite to optimize memory and draw calls
        Sprite circle64Sprite = CreateRegisteredSprite(
            CreateCircleTexture(64),
            new Rect(0, 0, 64, 64),
            new Vector2(0.5f, 0.5f)
        );

        // 4. Player Indicator in Center of Minimap (Directional Arrow)
        GameObject playerIconObj = new GameObject("PlayerIcon");
        playerIconObj.layer = 5;
        playerIconObj.transform.SetParent(minimapPanel.transform, false); // Outside mask so it sits on top

        playerIconRect = playerIconObj.AddComponent<RectTransform>();
        playerIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        playerIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        playerIconRect.pivot = new Vector2(0.5f, 0.5f);
        playerIconRect.sizeDelta = new Vector2(18f, 18f);
        playerIconRect.anchoredPosition = Vector2.zero;

        Image playerIconImage = playerIconObj.AddComponent<Image>();
        playerIconImage.sprite = circle64Sprite;
        playerIconImage.color = new Color(1.0f, 0.58f, 0.58f, 1f); // Vibrant pastel red/coral player icon

        // Add a subtle drop shadow to player icon
        playerIconObj.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

        // 5. Destination Icon Overlay
        destIconObj = new GameObject("DestinationIcon");
        destIconObj.layer = 5;
        destIconObj.transform.SetParent(minimapPanel.transform, false);

        destIconRect = destIconObj.AddComponent<RectTransform>();
        destIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        destIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        destIconRect.pivot = new Vector2(0.5f, 0.5f);
        destIconRect.sizeDelta = new Vector2(16f, 16f);
        destIconRect.anchoredPosition = Vector2.zero;

        destIconImage = destIconObj.AddComponent<Image>();
        destIconImage.sprite = circle64Sprite;
        destIconObj.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        destIconObj.SetActive(false);
    }

    private void SetupGPSLineRenderer() {
        GameObject lineObj = new GameObject("GPS_Path_Line");
        lineObj.layer = 31; // Set to Layer 31 (Minimap Only)
        // Rotate the line object so that its local Z axis points straight up.
        // Combined with LineAlignment.TransformZ, this forces the line to lie perfectly flat on the ground.
        lineObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        pathLineRenderer = lineObj.AddComponent<LineRenderer>();
        pathLineRenderer.alignment = LineAlignment.TransformZ;
        pathLineRenderer.useWorldSpace = true;
        pathLineRenderer.startWidth = lineWidth;
        pathLineRenderer.endWidth = lineWidth;
        pathLineRenderer.positionCount = 0;
        
        // Use standard URP Unlit or Sprites/Default unlit shader so the route glows bright neon and does not depend on scene lighting
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Lit");
        
        gpsLineMaterial = new Material(unlitShader);
        gpsLineMaterial.color = gpsPathColor;
        pathLineRenderer.sharedMaterial = gpsLineMaterial;

        // Ensure shadow casting/receiving is off for the line
        pathLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pathLineRenderer.receiveShadows = false;
    }

    private void UpdateCameraPosition() {
        if (minimapCamera == null || carTransform == null) return;
        // Follow player from above. Center on player.
        minimapCamera.transform.position = new Vector3(carTransform.position.x, 100f, carTransform.position.z);
        
        // Match minimap camera rotation with main camera's yaw so it rotates in sync or stays fixed when main camera is fixed.
        float targetYaw = carTransform.eulerAngles.y;
        if (CameraFollow.Instance != null) {
            targetYaw = CameraFollow.Instance.transform.eulerAngles.y;
        }
        
        // Rotate the camera around Y to match the view's heading.
        minimapCamera.transform.rotation = Quaternion.Euler(90f, targetYaw, 0f);
    }

    private void Update() {
        if (carTransform == null) return;

        // Update player icon rotation to match car's direction relative to minimap camera
        if (minimapCamera != null && playerIconRect != null) {
            float relativeYaw = carTransform.eulerAngles.y - minimapCamera.transform.eulerAngles.y;
            playerIconRect.localRotation = Quaternion.Euler(0f, 0f, -relativeYaw);
        }

        // 1. Query target and run GPS calculations
        PackageDeliverySystem deliverySystem = PackageDeliverySystem.Instance;
        DeliverySpot activeSpot = (deliverySystem != null) ? deliverySystem.ActiveSpot : null;
        bool isGameplayActive = deliverySystem != null && 
                                (deliverySystem.currentState == PackageDeliverySystem.DeliveryState.CollectingStamps || 
                                 deliverySystem.currentState == PackageDeliverySystem.DeliveryState.HeadingToFinalDestination);

        if (activeSpot != null && isGameplayActive) {
            // Update Destination UI Icon (DISABLED: requested no need to show blue/orange destination circle in minimap)
            /*
            destIconObj.SetActive(true);
            destIconImage.color = activeSpot.isPickupTarget ? pickupIconColor : deliveryIconColor;

            // Positioning icon relative to player on the minimap
            Vector3 diff = activeSpot.transform.position - carTransform.position;
            
            // Rotate the offset in the opposite direction of the car's heading to match the rotated camera
            float angleRad = -carTransform.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);
            float rx = diff.x * cos - diff.z * sin;
            float ry = diff.x * sin + diff.z * cos;

            // Scale world units to UI pixel offset
            // Minimap panel radius is ~84f pixels (half of mask inset 169f size)
            float mapRadius = 80f;
            float uiX = (rx / minimapOrthoSize) * mapRadius;
            float uiY = (ry / minimapOrthoSize) * mapRadius;

            // Clamp target icon to minimap boundary if off-screen
            float dist = Mathf.Sqrt(uiX * uiX + uiY * uiY);
            if (dist > mapRadius) {
                uiX = (uiX / dist) * mapRadius;
                uiY = (uiY / dist) * mapRadius;
                destIconRect.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            } else {
                destIconRect.localScale = Vector3.one;
            }

            destIconRect.anchoredPosition = new Vector2(uiX, uiY);
            */

            // Periodic GPS pathfinding recalculation
            if (Time.time >= nextGpsUpdateTime) {
                nextGpsUpdateTime = Time.time + gpsUpdateInterval;
                UpdateGPSPath(activeSpot.transform.position);
            }
        } else {
            // Disable navigation elements when there is no target
            if (destIconObj != null && destIconObj.activeSelf) destIconObj.SetActive(false);
            if (pathLineRenderer.positionCount > 0) pathLineRenderer.positionCount = 0;
        }
    }

    private System.Collections.IEnumerator MinimapRenderRoutine() {
        while (true) {
            if (minimapCamera != null && carTransform != null) {
                // Keep the camera locked to the player's position and rotation right before rendering
                UpdateCameraPosition();
                minimapCamera.Render();
            }
            float waitTime = minimapRenderFPS > 0f ? 1f / minimapRenderFPS : 0.05f;
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void UpdateGPSPath(Vector3 targetPos) {
        if (MapGenerator.Instance == null) return;

        // 1. Get snapped starting cell (car) and target cell
        GridPos startCell = GetClosestRoadCell(carTransform.position);
        GridPos targetCell = GetClosestRoadCell(targetPos);

        // 2. Perform A* pathfinding on the road grid cells
        List<GridPos> cellPath = FindPathOnRoads(startCell, targetCell);

        if (cellPath == null || cellPath.Count == 0) {
            // Draw direct line if pathfinding failed (as fallback)
            pathLineRenderer.positionCount = 2;
            pathLineRenderer.SetPosition(0, carTransform.position + Vector3.up * pathHeightOffset);
            pathLineRenderer.SetPosition(1, targetPos + Vector3.up * pathHeightOffset);
            return;
        }

        // 3. Convert grid cell path to world points
        // To make the path flow beautifully from the car's current position to the destination:
        // We prepend the car's exact position, and append the destination's exact position.
        List<Vector3> points = new List<Vector3>();
        
        // Add car position (projected to the road height)
        Vector3 carPathPos = carTransform.position;
        carPathPos.y = pathHeightOffset;
        points.Add(carPathPos);

        // Add centers of each road cell
        float cellSize = MapGenerator.Instance != null ? MapGenerator.Instance.cellSize : 2f;
        for (int i = 0; i < cellPath.Count; i++) {
            Vector3 cellWorld = new Vector3(cellPath[i].x * cellSize, pathHeightOffset, cellPath[i].z * cellSize);
            
            // Avoid adding cell point if it's extremely close to the car position to avoid jagged start line
            if (i == 0 && (carPathPos - cellWorld).sqrMagnitude < 1.0f) {
                continue;
            }
            points.Add(cellWorld);
        }

        // Add active spot target position
        Vector3 targetPathPos = targetPos;
        targetPathPos.y = pathHeightOffset;
        if (points.Count > 0 && (points[points.Count - 1] - targetPathPos).sqrMagnitude > 0.25f) {
            points.Add(targetPathPos);
        }

        // 4. Update LineRenderer points
        pathLineRenderer.positionCount = points.Count;
        pathLineRenderer.SetPositions(points.ToArray());
    }

    private GridPos GetClosestRoadCell(Vector3 pos) {
        float cellSize = MapGenerator.Instance != null ? MapGenerator.Instance.cellSize : 2f;
        GridPos snapped = new GridPos(
            Mathf.RoundToInt(pos.x / cellSize),
            Mathf.RoundToInt(pos.z / cellSize)
        );

        var roadCells = MapGenerator.Instance.RoadCells;
        var spotCells = MapGenerator.Instance.SpotCells;

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
        GridPos closest = snapped;
        float minD = float.MaxValue;
        foreach (var rc in roadCells) {
            float d = Mathf.Abs(pos.x - rc.x * cellSize) + Mathf.Abs(pos.z - rc.z * cellSize);
            if (d < minD) {
                minD = d;
                closest = rc;
            }
        }
        foreach (var sc in spotCells) {
            float d = Mathf.Abs(pos.x - sc.x * cellSize) + Mathf.Abs(pos.z - sc.z * cellSize);
            if (d < minD) {
                minD = d;
                closest = sc;
            }
        }
        return closest;
    }

    private List<GridPos> FindPathOnRoads(GridPos start, GridPos end) {
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

    // --- Texture Generator Helpers ---
    
    private Texture2D CreateCircleTexture(int size) {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius) {
                    tex.SetPixel(x, y, Color.white);
                } else {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return RegisterTexture(tex);
    }

    private Texture2D CreateCircleBorderTexture(int size, float thickness) {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 2f;

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius && dist >= radius - thickness) {
                    // Outer border ring color
                    tex.SetPixel(x, y, new Color(0.2f, 0.22f, 0.28f, 0.9f));
                } else if (dist < radius - thickness) {
                    // Solid fill translucent area for glassmorphism backing
                    tex.SetPixel(x, y, new Color(0.06f, 0.06f, 0.08f, 0.65f));
                } else {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return RegisterTexture(tex);
    }

    private Texture2D CreateCircleBorderOutlineTexture(int size, float thickness) {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 2f;

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius && dist >= radius - thickness) {
                    tex.SetPixel(x, y, Color.white); // colored dynamically via UI Image tint
                } else {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return RegisterTexture(tex);
    }

    private Texture2D CreateTriangleTexture(int size) {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size / 2f;

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                // Triangle pointing up. We fill pixel if x is within boundaries
                float bounds = (y / (float)size) * half;
                if (x >= half - bounds && x <= half + bounds) {
                    tex.SetPixel(x, y, Color.white);
                } else {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return RegisterTexture(tex);
    }

    private void OnDestroy() {
        if (minimapRT != null) {
            minimapRT.Release();
        }
        if (gpsLineMaterial != null) {
            Destroy(gpsLineMaterial);
        }
        if (generatedSprites != null) {
            foreach (var sprite in generatedSprites) {
                if (sprite != null) {
                    Destroy(sprite);
                }
            }
            generatedSprites.Clear();
        }
        if (generatedTextures != null) {
            foreach (var tex in generatedTextures) {
                if (tex != null) {
                    Destroy(tex);
                }
            }
            generatedTextures.Clear();
        }
    }
}
