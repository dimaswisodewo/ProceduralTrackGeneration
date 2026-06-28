using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class DeliverySpot : MonoBehaviour {
    public bool isDeliveryTarget = false;
    public bool isPickupTarget = false;

    private GameObject hologramContainer;
    private GameObject ringVisual;
    private Material ringMaterial;
    private Material frameMaterial;
    private GameObject scannerPlane;
    private List<Material> spotMaterials = new List<Material>();
    private Tween pulseTween;
    private Tween scannerTween;

    private void Start() {
        // 1. Create a dedicated trigger collider for checking player arrival
        // This avoids modifying any existing model colliders.
        BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 3f, 3f);
        trigger.center = new Vector3(0f, 1.5f, 0f);

        // 2. Create the flat visual ring representing the zone
        CreateVisualRing();

        // 3. Initialize the 3D spot object (cube/prefab) materials with SpotMaterial copy so they can glow
        InitializeSpotMaterials();
    }

    private void CreateVisualRing() {
        // Create a container for all hologram components
        hologramContainer = new GameObject("HologramContainer");
        hologramContainer.transform.parent = this.transform;
        hologramContainer.transform.localPosition = Vector3.zero;
        hologramContainer.transform.localRotation = Quaternion.identity;
        hologramContainer.transform.localScale = Vector3.one;

        // 1. Create the central transparent volumetric cube
        ringVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(ringVisual.GetComponent<Collider>());
        ringVisual.transform.parent = hologramContainer.transform;
        ringVisual.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        ringVisual.transform.localScale = new Vector3(3f, 5f, 3f);
        ringVisual.transform.localRotation = Quaternion.identity;
        ringVisual.layer = 30; // Main Camera Only / Hide from Minimap

        Renderer renderer = ringVisual.GetComponent<Renderer>();
        if (renderer != null) {
            ringMaterial = VisualEffectUtility.GetSpotMaterial();
            renderer.sharedMaterial = ringMaterial;
        }

        // 2. Instantiate a separate material copy for the glowing frame parts
        frameMaterial = VisualEffectUtility.GetSpotMaterial();

        // 3. Bottom Cap (flat cube frame at ground level)
        GameObject bottomCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(bottomCap.GetComponent<Collider>());
        bottomCap.transform.parent = hologramContainer.transform;
        bottomCap.transform.localPosition = new Vector3(0f, 0.025f, 0f);
        bottomCap.transform.localScale = new Vector3(3.05f, 0.05f, 3.05f);
        bottomCap.transform.localRotation = Quaternion.identity;
        bottomCap.layer = 30;
        bottomCap.GetComponent<Renderer>().sharedMaterial = frameMaterial;

        // 4. Top Cap (flat cube frame at top height)
        GameObject topCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(topCap.GetComponent<Collider>());
        topCap.transform.parent = hologramContainer.transform;
        topCap.transform.localPosition = new Vector3(0f, 4.975f, 0f);
        topCap.transform.localScale = new Vector3(3.05f, 0.05f, 3.05f);
        topCap.transform.localRotation = Quaternion.identity;
        topCap.layer = 30;
        topCap.GetComponent<Renderer>().sharedMaterial = frameMaterial;

        // 5. Four vertical corner pillars
        float[] coords = { -1.5f, 1.5f };
        foreach (float x in coords) {
            foreach (float z in coords) {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(pillar.GetComponent<Collider>());
                pillar.transform.parent = hologramContainer.transform;
                pillar.transform.localPosition = new Vector3(x, 2.5f, z);
                pillar.transform.localScale = new Vector3(0.08f, 5.0f, 0.08f);
                pillar.transform.localRotation = Quaternion.identity;
                pillar.layer = 30;
                pillar.GetComponent<Renderer>().sharedMaterial = frameMaterial;
            }
        }

        // 6. Scanner Plane (animates up and down inside the cube)
        scannerPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(scannerPlane.GetComponent<Collider>());
        scannerPlane.transform.parent = hologramContainer.transform;
        scannerPlane.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        scannerPlane.transform.localScale = new Vector3(3.02f, 0.04f, 3.02f);
        scannerPlane.transform.localRotation = Quaternion.identity;
        scannerPlane.layer = 30;
        scannerPlane.GetComponent<Renderer>().sharedMaterial = frameMaterial;

        // Animate scanner plane bouncing up and down
        scannerTween = scannerPlane.transform.DOLocalMoveY(4.9f, 2.0f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutQuad);

        UpdateVisuals();
    }

    private void InitializeSpotMaterials() {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) {
            // Skip any renderers belonging to the hologram container or its hierarchy
            if (hologramContainer != null && r.transform.IsChildOf(hologramContainer.transform)) {
                continue;
            }
            
            // Assign a unique copy of the spot material
            Material mat = VisualEffectUtility.GetSpotMaterial();
            
            // Configure it to be completely opaque so it contrasts with other buildings
            VisualEffectUtility.ConfigureOpaqueMaterial(mat);
            
            r.sharedMaterial = mat;
            spotMaterials.Add(mat);
        }
        UpdateVisuals();
    }

    public void SetAsTarget(bool isTarget, bool isPickup = false) {
        isDeliveryTarget = isTarget && !isPickup;
        isPickupTarget = isTarget && isPickup;
        UpdateVisuals();
    }

    private void UpdateVisuals() {
        if (ringMaterial == null || frameMaterial == null) return;

        if (pulseTween != null) {
            pulseTween.Kill();
            pulseTween = null;
        }

        Color ringColor;
        Color ringEmissionColor = Color.black;
        Color frameColor;
        Color frameEmissionColor = Color.black;
        Color spotColor;
        bool enableHologramEmission = false;

        if (isPickupTarget) {
            // Hologram box: very transparent lilac
            ringColor = new Color(0.85f, 0.68f, 1.0f, 0.04f); 
            ringEmissionColor = new Color(0.85f, 0.68f, 1.0f) * 0.2f; // Soft volume glow
            
            // Hologram frame: more solid, bright glowing lilac/purple frame & scanner
            frameColor = new Color(0.85f, 0.68f, 1.0f, 0.4f);
            frameEmissionColor = new Color(0.85f, 0.68f, 1.0f) * 2.5f; // Strong outline/scanner glow
            
            // Spot 3D: Pretty vibrant pastel coral/orchid pink (no emission)
            spotColor = new Color(0.95f, 0.3f, 0.6f, 1.0f); 
            enableHologramEmission = true;
        } else if (isDeliveryTarget) {
            // Hologram box: very transparent mint/cyan
            ringColor = new Color(0.55f, 0.9f, 1.0f, 0.04f); 
            ringEmissionColor = new Color(0.55f, 0.9f, 1.0f) * 0.2f; // Soft volume glow
            
            // Hologram frame: more solid, bright glowing mint/cyan frame & scanner
            frameColor = new Color(0.55f, 0.9f, 1.0f, 0.4f);
            frameEmissionColor = new Color(0.55f, 0.9f, 1.0f) * 2.5f; // Strong outline/scanner glow
            
            // Spot 3D: Pretty vibrant ocean teal/cyan (no emission)
            spotColor = new Color(0.1f, 0.75f, 0.8f, 1.0f); 
            enableHologramEmission = true;
        } else {
            // Inactive spot
            ringColor = new Color(0.6f, 0.6f, 0.6f, 0.01f);
            ringEmissionColor = Color.black;
            
            frameColor = new Color(0.6f, 0.6f, 0.6f, 0.05f);
            frameEmissionColor = Color.black;
            
            // Spot 3D: Muted slate gray (no emission)
            spotColor = new Color(0.4f, 0.45f, 0.5f, 1.0f); 
            enableHologramEmission = false;
        }

        // Apply to the hologram box (transparent)
        VisualEffectUtility.ApplyMaterialColor(ringMaterial, ringColor, ringEmissionColor, enableHologramEmission);

        // Apply to the hologram frame/scanner
        VisualEffectUtility.ApplyMaterialColor(frameMaterial, frameColor, frameEmissionColor, enableHologramEmission);

        // Apply to the main spot 3D object (opaque, no emission)
        if (spotMaterials != null) {
            foreach (var mat in spotMaterials) {
                VisualEffectUtility.ApplyMaterialColor(mat, spotColor, Color.black, false);
            }
        }

        bool isTarget = isPickupTarget || isDeliveryTarget;
        if (hologramContainer != null) {
            hologramContainer.SetActive(isTarget);
            
            if (isTarget) {
                // Animate scale of the entire hologram container to pulsate in 3D (unified breathing effect)
                hologramContainer.transform.localScale = Vector3.one;
                pulseTween = hologramContainer.transform.DOScale(new Vector3(1.05f, 1.03f, 1.05f), 1.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            } else {
                hologramContainer.transform.localScale = Vector3.one;
            }
        }

        if (scannerTween != null) {
            if (isTarget) {
                scannerTween.Play();
            } else {
                scannerTween.Pause();
            }
        }
    }

    private void OnTriggerEnter(Collider other) {
        // Find the CarController on the entering object or its parents
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null) {
            if (PackageDeliverySystem.Instance != null) {
                PackageDeliverySystem.Instance.OnCarEnteredSpot(this);
            }
        }
    }

    private void OnDestroy() {
        if (pulseTween != null) {
            pulseTween.Kill();
        }
        if (scannerTween != null) {
            scannerTween.Kill();
        }
        if (ringMaterial != null) {
            Destroy(ringMaterial);
        }
        if (frameMaterial != null) {
            Destroy(frameMaterial);
        }
        if (spotMaterials != null) {
            foreach (var mat in spotMaterials) {
                if (mat != null) {
                    Destroy(mat);
                }
            }
            spotMaterials.Clear();
        }
    }
}

