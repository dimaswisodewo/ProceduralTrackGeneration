using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class DeliverySpot : MonoBehaviour {
    public bool isDeliveryTarget = false;
    public bool isPickupTarget = false;

    private GameObject ringVisual;
    private Material ringMaterial;
    private List<Material> spotMaterials = new List<Material>();
    private Tween pulseTween;

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
        // Create a cube to act as a hologram light box
        ringVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(ringVisual.GetComponent<Collider>()); // Visual only, remove physics

        ringVisual.transform.parent = this.transform;
        ringVisual.transform.localPosition = new Vector3(0f, 2.5f, 0f); // Centered at half height to sit flat on ground
        ringVisual.transform.localScale = new Vector3(3f, 5f, 3f); // Taller cube to cover the entire spot
        ringVisual.transform.localRotation = Quaternion.identity;
        ringVisual.layer = 30; // Main Camera Only / Hide from Minimap

        Renderer renderer = ringVisual.GetComponent<Renderer>();
        if (renderer != null) {
            ringMaterial = VisualEffectUtility.GetSpotMaterial();
            renderer.sharedMaterial = ringMaterial;
        }

        UpdateVisuals();
    }

    private void InitializeSpotMaterials() {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) {
            if (ringVisual != null && r.gameObject == ringVisual) {
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
        if (ringMaterial == null) return;

        if (pulseTween != null) {
            pulseTween.Kill();
            pulseTween = null;
        }

        Color ringColor;
        Color ringEmissionColor = Color.black;
        Color spotColor;
        Color spotEmissionColor = Color.black;
        bool enableEmission = false;

        if (isPickupTarget) {
            // Hologram box is very transparent lilac, spot 3D is a highly vibrant neon pink
            ringColor = new Color(0.85f, 0.68f, 1.0f, 0.08f); // High transparency Lilac Hologram
            ringEmissionColor = new Color(0.85f, 0.68f, 1.0f) * 0.4f; // Soft hologram outline glow (lowered emission)
            
            spotColor = new Color(1.0f, 0.15f, 0.75f, 1.0f); // Vibrant Solid Neon Pink/Magenta for spot 3D
            spotEmissionColor = new Color(1.0f, 0.15f, 0.75f) * 6.0f; // High-contrast intense HDR emission
            enableEmission = true;
        } else if (isDeliveryTarget) {
            // Hologram box is very transparent peach, spot 3D is a highly vibrant electric cyan
            ringColor = new Color(1.0f, 0.75f, 0.55f, 0.08f); // High transparency Peach Hologram
            ringEmissionColor = new Color(1.0f, 0.75f, 0.55f) * 0.4f; // Soft hologram outline glow (lowered emission)
            
            spotColor = new Color(0.15f, 0.75f, 1.0f, 1.0f); // Vibrant Solid Electric Cyan/Blue for spot 3D
            spotEmissionColor = new Color(0.15f, 0.75f, 1.0f) * 6.0f; // High-contrast intense HDR emission
            enableEmission = true;
        } else {
            // Dim, semi-transparent light grey for inactive spots
            ringColor = new Color(0.6f, 0.6f, 0.6f, 0.02f);
            ringEmissionColor = Color.black;
            
            spotColor = new Color(0.35f, 0.35f, 0.35f, 1.0f); // Make inactive spot opaque grey
            spotEmissionColor = Color.black;
            enableEmission = false;
        }

        // Apply to the hologram box (transparent)
        VisualEffectUtility.ApplyMaterialColor(ringMaterial, ringColor, ringEmissionColor, enableEmission);

        // Apply to the main spot 3D object (opaque)
        if (spotMaterials != null) {
            foreach (var mat in spotMaterials) {
                VisualEffectUtility.ApplyMaterialColor(mat, spotColor, spotEmissionColor, enableEmission);
            }
        }

        if (ringVisual != null) {
            if (isPickupTarget || isDeliveryTarget) {
                // Animate scale to pulsate in 3D (hologram box breathing effect)
                ringVisual.transform.localScale = new Vector3(3f, 5f, 3f);
                pulseTween = ringVisual.transform.DOScale(new Vector3(3.25f, 5.25f, 3.25f), 1.2f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            } else {
                // Settle back to default scale for inactive zones
                ringVisual.transform.localScale = new Vector3(3f, 5f, 3f);
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
        if (ringMaterial != null) {
            Destroy(ringMaterial);
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
